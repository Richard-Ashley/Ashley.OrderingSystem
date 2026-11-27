using Contracts.Events;
using Contracts.Inventory;
using MassTransit;
using OrdersService.Clients;
using OrdersService.Data;
using OrdersService.Models;

namespace OrdersService.Services;

public sealed class OrderService : IOrderService
{
    private readonly OrdersDbContext _db;
    private readonly IInventoryClient _inventory;
    private readonly IPublishEndpoint _publisher;
    private readonly ILogger<OrderService> _logger;
    private readonly TimeSpan _publishTimeout;

    public OrderService(
        OrdersDbContext db,
        IInventoryClient inventory,
        IPublishEndpoint publisher,
        IConfiguration configuration,
        ILogger<OrderService> logger)
    {
        _db = db;
        _inventory = inventory;
        _publisher = publisher;
        _logger = logger;
        _publishTimeout = TimeSpan.FromSeconds(configuration.GetValue("RabbitMq:PublishTimeoutSeconds", 5));
    }

    public async Task<PlaceOrderResult> PlaceOrder(PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        StockCheckResponse stockCheck;
        try
        {
            stockCheck = await _inventory.CheckStock(
                new StockCheckRequest(request.ProductId, request.Quantity),
                cancellationToken);
        }
        catch (InventoryUnavailableException ex)
        {
            _logger.LogError(ex, "Could not reach InventoryService for order attempt (product {ProductId})", request.ProductId);
            return new PlaceOrderResult(
                Success: false,
                OrderId: null,
                Reason: "Inventory service is unavailable. Please try again later.",
                StatusCode: 503);
        }

        if (!stockCheck.InStock || stockCheck.AvailableQuantity < request.Quantity)
        {
            _logger.LogInformation(
                "Rejected order for product {ProductId}: requested {Requested}, available {Available}",
                request.ProductId, request.Quantity, stockCheck.AvailableQuantity);

            return new PlaceOrderResult(
                Success: false,
                OrderId: null,
                Reason: $"Insufficient stock for product {request.ProductId}: requested {request.Quantity}, available {stockCheck.AvailableQuantity}.");
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            CustomerEmail = request.CustomerEmail.Trim().ToLowerInvariant(),
            Status = OrderStatus.Placed,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Placed order {OrderId} for {CustomerEmail}", order.Id, order.CustomerEmail);

        // Known limitation: plain save-then-publish. If the process dies or the broker is
        // unreachable after the save, the event is lost and Inventory never decrements.
        // The production fix is a transactional outbox (MassTransit.EntityFrameworkCore).
        // The publish is time-boxed: with the broker unreachable MassTransit would otherwise
        // wait for a very long time and hold the HTTP request open.
        using var publishCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        publishCts.CancelAfter(_publishTimeout);
        try
        {
            await _publisher.Publish(
                new OrderPlacedEvent(order.Id, order.ProductId, order.Quantity, order.CustomerEmail, order.CreatedAt),
                publishCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order {OrderId} was saved but OrderPlaced could not be published", order.Id);
        }

        return new PlaceOrderResult(Success: true, OrderId: order.Id, Reason: null);
    }
}
