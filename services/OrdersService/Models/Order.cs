namespace OrdersService.Models;

public sealed class Order
{
    public Guid Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public enum OrderStatus
{
    Placed = 0,
    Cancelled = 1,
    Fulfilled = 2
}
