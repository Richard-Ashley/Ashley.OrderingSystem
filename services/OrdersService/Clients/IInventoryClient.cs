using Contracts.Inventory;

namespace OrdersService.Clients;

public interface IInventoryClient
{
    Task<StockCheckResponse> CheckStock(
        StockCheckRequest request,
        CancellationToken cancellationToken = default);
}
