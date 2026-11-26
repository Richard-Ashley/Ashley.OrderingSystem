using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Data;

public sealed class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(p => p.StockQuantity).IsRequired();

            entity.Property(p => p.UpdatedAt).IsRequired();
        });

        var seedTimestamp = new DateTimeOffset(2026, 11, 26, 0, 0, 0, TimeSpan.Zero);

        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1,  Name = "Wireless mouse",           StockQuantity = 50,  UpdatedAt = seedTimestamp },
            new Product { Id = 2,  Name = "Mechanical keyboard",      StockQuantity = 25,  UpdatedAt = seedTimestamp },
            new Product { Id = 3,  Name = "USB-C hub",                StockQuantity = 100, UpdatedAt = seedTimestamp },
            new Product { Id = 4,  Name = "27-inch monitor",          StockQuantity = 15,  UpdatedAt = seedTimestamp },
            new Product { Id = 5,  Name = "Desk lamp",                StockQuantity = 40,  UpdatedAt = seedTimestamp },
            new Product { Id = 6,  Name = "Noise-canceling headset",  StockQuantity = 8,   UpdatedAt = seedTimestamp },
            new Product { Id = 7,  Name = "Limited edition mousepad", StockQuantity = 0,   UpdatedAt = seedTimestamp },
            new Product { Id = 8,  Name = "Standing desk converter",  StockQuantity = 12,  UpdatedAt = seedTimestamp },
            new Product { Id = 9,  Name = "Webcam cover slider",      StockQuantity = 200, UpdatedAt = seedTimestamp },
            new Product { Id = 10, Name = "Laptop stand (aluminum)",  StockQuantity = 30,  UpdatedAt = seedTimestamp });
    }
}
