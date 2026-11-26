using Microsoft.EntityFrameworkCore;
using OrdersService.Models;

namespace OrdersService.Data;

public sealed class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);

            entity.Property(o => o.CustomerEmail)
                .IsRequired()
                .HasMaxLength(320);

            entity.Property(o => o.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(o => o.CreatedAt).IsRequired();

            entity.HasIndex(o => o.CustomerEmail);
            entity.HasIndex(o => o.CreatedAt);
        });
    }
}
