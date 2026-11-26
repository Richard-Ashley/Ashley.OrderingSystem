namespace OrdersService.Models;

public sealed record PlaceOrderRequest(
    int ProductId,
    int Quantity,
    string CustomerEmail);

public sealed record PlaceOrderResponse(
    Guid OrderId,
    string Status);
