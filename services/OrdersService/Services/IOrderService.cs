using OrdersService.Models;

namespace OrdersService.Services;

public interface IOrderService
{
    Task<PlaceOrderResult> PlaceOrder(PlaceOrderRequest request, CancellationToken cancellationToken);
}

public sealed record PlaceOrderResult(bool Success, Guid? OrderId, string? Reason);
