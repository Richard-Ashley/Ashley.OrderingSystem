using Contracts.Inventory;
using OrdersService.Clients;
using OrdersService.Data;
using OrdersService.Models;

namespace OrdersService.Services;

public sealed class OrderService : IOrderService
{
    private readonly OrdersDbContext _db;
    private readonly IInventoryClient _inventory;
    private readonly ILogger<OrderService> _logger;

    public OrderService(OrdersDbContext db, IInventoryClient inventory, ILogger<OrderService> logger)
    {
        _db = db;
        _inventory = inventory;
        _logger = logger;
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

        return new PlaceOrderResult(Success: true, OrderId: order.Id, Reason: null);
    }
}
