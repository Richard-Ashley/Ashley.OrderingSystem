namespace InventoryService.Models;

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
