namespace Contracts.Events;

/// <summary>Published by OrdersService after an order has been saved.</summary>
public sealed record OrderPlacedEvent(
    Guid OrderId,
    int ProductId,
    int Quantity,
    string CustomerEmail,
    DateTimeOffset PlacedAt);
