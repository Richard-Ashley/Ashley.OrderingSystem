namespace Contracts.Inventory;

public sealed record StockCheckRequest(int ProductId, int Quantity);

public sealed record StockCheckResponse(bool InStock, int AvailableQuantity, string ProductName);
