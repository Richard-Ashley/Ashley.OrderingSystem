using Contracts.Events;
using InventoryService.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Consumers;

/// <summary>
/// Decrements stock for a placed order. Idempotent per message id, clamps stock at zero
/// (logging any overcommit), and publishes StockLow when stock is at or below the threshold.
/// </summary>
public sealed class OrderPlacedConsumer : IConsumer<OrderPlacedEvent>
{
    private readonly InventoryDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrderPlacedConsumer> _logger;

    public OrderPlacedConsumer(
        InventoryDbContext db,
        IConfiguration configuration,
        ILogger<OrderPlacedConsumer> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var message = context.Message;
        var cancellationToken = context.CancellationToken;
        var messageId = context.MessageId
            ?? throw new InvalidOperationException("OrderPlaced message has no MessageId.");

        if (await _db.ProcessedMessages.AnyAsync(m => m.MessageId == messageId, cancellationToken))
        {
            _logger.LogInformation(
                "Skipping duplicate OrderPlaced message {MessageId} for order {OrderId}",
                messageId, message.OrderId);
            return;
        }

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == message.ProductId, cancellationToken);
        if (product is null)
        {
            // Retrying cannot fix an unknown product, so record the message and move on.
            _logger.LogWarning(
                "OrderPlaced for order {OrderId} references unknown product {ProductId}",
                message.OrderId, message.ProductId);
            _db.ProcessedMessages.Add(new Models.ProcessedMessage { MessageId = messageId, ProcessedAt = DateTimeOffset.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (message.Quantity > product.StockQuantity)
        {
            _logger.LogWarning(
                "Overcommit on product {ProductId}: order {OrderId} wants {Requested} but only {Available} in stock. Clamping to zero.",
                product.Id, message.OrderId, message.Quantity, product.StockQuantity);
        }

        product.StockQuantity = Math.Max(0, product.StockQuantity - message.Quantity);
        product.UpdatedAt = DateTimeOffset.UtcNow;

        // Stock update and idempotency record commit in the same transaction.
        _db.ProcessedMessages.Add(new Models.ProcessedMessage { MessageId = messageId, ProcessedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Decremented product {ProductId} by {Quantity} for order {OrderId}; {Remaining} remaining",
            product.Id, message.Quantity, message.OrderId, product.StockQuantity);

        var threshold = _configuration.GetValue("Inventory:LowStockThreshold", 10);
        if (product.StockQuantity <= threshold)
        {
            await context.Publish(new StockLowEvent(
                product.Id, product.Name, product.StockQuantity, threshold, DateTimeOffset.UtcNow));
        }
    }
}
