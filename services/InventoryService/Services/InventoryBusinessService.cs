using Contracts.Inventory;
using InventoryService.Data;
using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Services;

public sealed class InventoryBusinessService : IInventoryService
{
    private readonly InventoryDbContext _db;
    private readonly ILogger<InventoryBusinessService> _logger;

    public InventoryBusinessService(InventoryDbContext db, ILogger<InventoryBusinessService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<StockCheckResponse?> CheckStock(StockCheckRequest request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            _logger.LogInformation("Stock check failed: product {ProductId} not found", request.ProductId);
            return null;
        }

        return new StockCheckResponse(
            InStock: product.StockQuantity >= request.Quantity,
            AvailableQuantity: product.StockQuantity,
            ProductName: product.Name);
    }

    public Task<Product?> GetProduct(int productId, CancellationToken cancellationToken) =>
        _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

    public async Task<IReadOnlyList<Product>> ListProducts(CancellationToken cancellationToken) =>
        await _db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync(cancellationToken);
}
