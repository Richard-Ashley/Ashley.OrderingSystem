using Contracts.Events;
using MassTransit;

namespace NotificationService.Consumers;

/// <summary>Pretends to send an order confirmation. In this demo it only logs.</summary>
public sealed class OrderPlacedConsumer : IConsumer<OrderPlacedEvent>
{
    private readonly ILogger<OrderPlacedConsumer> _logger;

    public OrderPlacedConsumer(ILogger<OrderPlacedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Order confirmation (simulated) to {CustomerEmail}: order {OrderId}, {Quantity} x product {ProductId}",
            message.CustomerEmail, message.OrderId, message.Quantity, message.ProductId);

        return Task.CompletedTask;
    }
}
