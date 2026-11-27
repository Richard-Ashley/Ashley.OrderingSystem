using Contracts.Events;
using MassTransit;

namespace OrdersService.Consumers;

/// <summary>
/// Reacts to StockLow events from InventoryService. In this demo it only logs;
/// a real implementation might restrict new orders for the product.
/// </summary>
public sealed class StockLowConsumer : IConsumer<StockLowEvent>
{
    private readonly ILogger<StockLowConsumer> _logger;

    public StockLowConsumer(ILogger<StockLowConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<StockLowEvent> context)
    {
        var message = context.Message;
        _logger.LogWarning(
            "Low stock for product {ProductId} ({ProductName}): {Remaining} left (threshold {Threshold})",
            message.ProductId, message.ProductName, message.RemainingQuantity, message.Threshold);

        return Task.CompletedTask;
    }
}
