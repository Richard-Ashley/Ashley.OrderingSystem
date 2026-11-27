using Contracts.Events;
using MassTransit;

namespace NotificationService.Consumers;

/// <summary>Pretends to send a low-stock alert to operations. In this demo it only logs.</summary>
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
            "Low stock alert (simulated): product {ProductId} ({ProductName}) has {Remaining} left (threshold {Threshold})",
            message.ProductId, message.ProductName, message.RemainingQuantity, message.Threshold);

        return Task.CompletedTask;
    }
}
