using System.Net.Http.Json;
using Contracts.Inventory;

namespace OrdersService.Clients;

public sealed class HttpInventoryClient : IInventoryClient
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpInventoryClient> _logger;

    public HttpInventoryClient(HttpClient http, ILogger<HttpInventoryClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<StockCheckResponse> CheckStock(
        StockCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                "/api/inventory/check",
                request,
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("InventoryService returned 404 for product {ProductId}", request.ProductId);
                return new StockCheckResponse(
                    InStock: false,
                    AvailableQuantity: 0,
                    ProductName: $"Unknown product {request.ProductId}");
            }

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<StockCheckResponse>(cancellationToken);
            return payload
                ?? throw new InvalidOperationException("InventoryService returned an empty body.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "InventoryService call timed out for product {ProductId}", request.ProductId);
            throw new InventoryUnavailableException("InventoryService timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "InventoryService call failed for product {ProductId}", request.ProductId);
            throw new InventoryUnavailableException("InventoryService is unreachable.", ex);
        }
    }
}

public sealed class InventoryUnavailableException : Exception
{
    public InventoryUnavailableException(string message, Exception inner) : base(message, inner) { }
}
