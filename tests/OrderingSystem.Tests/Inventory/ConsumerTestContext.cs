using Contracts.Events;
using InventoryService.Consumers;
using InventoryService.Data;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OrderingSystem.Tests.Inventory;

/// <summary>
/// Runs OrderPlacedConsumer on the MassTransit in-memory test harness against an in-memory SQLite
/// database seeded exactly like the real one (10 products: p1=50, p2=25, p6=8, p7=0, ...).
/// SQLite is used instead of the EF InMemory provider because it enforces keys and constraints.
/// </summary>
internal sealed class ConsumerTestContext : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly HashSet<Guid> _publishedIds = [];

    private ConsumerTestContext(SqliteConnection connection, ServiceProvider provider, ITestHarness harness)
    {
        _connection = connection;
        _provider = provider;
        Harness = harness;
    }

    public ITestHarness Harness { get; }

    public static async Task<ConsumerTestContext> Create(int lowStockThreshold = 10)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Inventory:LowStockThreshold"] = lowStockThreshold.ToString()
            })
            .Build();

        var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddDbContext<InventoryDbContext>(options => options.UseSqlite(connection))
            .AddMassTransitTestHarness(x => x.AddConsumer<OrderPlacedConsumer>())
            .BuildServiceProvider(true);

        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.EnsureCreatedAsync();
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        return new ConsumerTestContext(connection, provider, harness);
    }

    /// <summary>Publishes an OrderPlaced event and waits until the consumer has handled it.</summary>
    public async Task Publish(int productId, int quantity, Guid? messageId = null)
    {
        var consumed = Harness.GetConsumerHarness<OrderPlacedConsumer>().Consumed;
        var before = consumed.Select<OrderPlacedEvent>().Count();
        var id = messageId ?? Guid.NewGuid();
        var isRepeat = !_publishedIds.Add(id);

        await Harness.Bus.Publish(
            new OrderPlacedEvent(Guid.NewGuid(), productId, quantity, "test@example.com", DateTimeOffset.UtcNow),
            context => context.MessageId = id);

        if (isRepeat)
        {
            // The harness records consumed messages by MessageId, so a redelivery adds no new
            // entry to count. Wait for the bus to go quiet instead.
            await Harness.InactivityTask;
            return;
        }

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (consumed.Select<OrderPlacedEvent>().Count() <= before)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("OrderPlacedConsumer did not handle the message in time.");

            await Task.Delay(25);
        }
    }

    public async Task<int> StockOf(int productId)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        return product.StockQuantity;
    }

    public async Task<int> ProcessedMessageCount()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        return await db.ProcessedMessages.CountAsync();
    }

    /// <summary>
    /// True if the consumer threw while handling any message. A faulted consumer publishes a
    /// Fault message; the consumed list is not reliable for this because the harness keys it by
    /// MessageId and records nothing for a redelivered id.
    /// </summary>
    public bool ConsumerFaulted() =>
        Harness.Published.Select<Fault<OrderPlacedEvent>>().Any();

    public IReadOnlyList<StockLowEvent> PublishedStockLow() =>
        Harness.Published.Select<StockLowEvent>().Select(m => m.Context.Message).ToList();

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
