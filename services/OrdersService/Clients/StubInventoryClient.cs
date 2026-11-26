using Contracts.Inventory;

namespace OrdersService.Clients;

/// <summary>
/// Temporary hardcoded implementation used in Wave B before InventoryService exists.
/// Returns "in stock" for every product. Wave C replaces this with HttpInventoryClient.
/// </summary>
public sealed class StubInventoryClient : IInventoryClient
{
    public Task<StockCheckResponse> CheckStock(
        StockCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new StockCheckResponse(
            InStock: true,
            AvailableQuantity: 100,
            ProductName: $"Stub product {request.ProductId}");

        return Task.FromResult(response);
    }
}
