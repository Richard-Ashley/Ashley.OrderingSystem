namespace Contracts.Events;

/// <summary>
/// Published by InventoryService when a product's stock is at or below the configured
/// threshold after a decrement (level-triggered: every decrement on a low product publishes).
/// </summary>
public sealed record StockLowEvent(
    int ProductId,
    string ProductName,
    int RemainingQuantity,
    int Threshold,
    DateTimeOffset OccurredAt);
