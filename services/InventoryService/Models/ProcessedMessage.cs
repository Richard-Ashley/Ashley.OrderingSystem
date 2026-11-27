namespace InventoryService.Models;

/// <summary>
/// Records message ids already handled by a consumer so redelivered messages
/// (RabbitMQ is at-least-once) do not decrement stock twice.
/// </summary>
public sealed class ProcessedMessage
{
    public Guid MessageId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}
