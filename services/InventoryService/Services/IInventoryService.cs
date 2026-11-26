using Contracts.Inventory;
using InventoryService.Models;

namespace InventoryService.Services;

public interface IInventoryService
{
    Task<StockCheckResponse?> CheckStock(StockCheckRequest request, CancellationToken cancellationToken);

    Task<Product?> GetProduct(int productId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Product>> ListProducts(CancellationToken cancellationToken);
}
