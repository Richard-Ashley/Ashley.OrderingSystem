using FluentAssertions;

namespace OrderingSystem.Tests.Inventory;

public sealed class OrderPlacedConsumerTests
{
    [Fact]
    public async Task First_delivery_decrements_stock_without_low_stock_event()
    {
        await using var context = await ConsumerTestContext.Create();

        await context.Publish(productId: 1, quantity: 2); // product 1 starts at 50

        (await context.StockOf(1)).Should().Be(48);
        context.PublishedStockLow().Should().BeEmpty();
    }

    [Fact]
    public async Task Duplicate_message_id_decrements_stock_only_once()
    {
        await using var context = await ConsumerTestContext.Create();
        var messageId = Guid.NewGuid();

        await context.Publish(productId: 3, quantity: 5, messageId); // product 3 starts at 100
        await context.Publish(productId: 3, quantity: 5, messageId);

        (await context.StockOf(3)).Should().Be(95);
        (await context.ProcessedMessageCount()).Should().Be(1);

        // The duplicate must be skipped cleanly, not rejected by the primary key as a fault.
        context.ConsumerFaulted().Should().BeFalse();
    }

    [Fact]
    public async Task Different_message_ids_each_decrement()
    {
        await using var context = await ConsumerTestContext.Create();

        await context.Publish(productId: 3, quantity: 5);
        await context.Publish(productId: 3, quantity: 5);

        (await context.StockOf(3)).Should().Be(90);
        (await context.ProcessedMessageCount()).Should().Be(2);
    }

    [Fact]
    public async Task Decrement_below_zero_clamps_to_zero_and_publishes_low_stock()
    {
        await using var context = await ConsumerTestContext.Create();

        await context.Publish(productId: 7, quantity: 3); // product 7 starts at 0

        (await context.StockOf(7)).Should().Be(0);
        var lowStock = context.PublishedStockLow().Should().ContainSingle().Subject;
        lowStock.ProductId.Should().Be(7);
        lowStock.RemainingQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Decrement_crossing_threshold_publishes_low_stock()
    {
        await using var context = await ConsumerTestContext.Create();

        await context.Publish(productId: 2, quantity: 20); // product 2 starts at 25

        (await context.StockOf(2)).Should().Be(5);
        var lowStock = context.PublishedStockLow().Should().ContainSingle().Subject;
        lowStock.ProductId.Should().Be(2);
        lowStock.RemainingQuantity.Should().Be(5);
        lowStock.Threshold.Should().Be(10);
    }

    [Fact]
    public async Task Decrement_landing_exactly_on_threshold_publishes_low_stock()
    {
        await using var context = await ConsumerTestContext.Create();

        await context.Publish(productId: 2, quantity: 15); // 25 - 15 = 10, exactly the threshold

        (await context.StockOf(2)).Should().Be(10);
        context.PublishedStockLow().Should().ContainSingle()
            .Which.RemainingQuantity.Should().Be(10);
    }

    [Fact]
    public async Task Decrement_landing_one_above_threshold_does_not_publish_low_stock()
    {
        await using var context = await ConsumerTestContext.Create();

        await context.Publish(productId: 2, quantity: 14); // 25 - 14 = 11

        (await context.StockOf(2)).Should().Be(11);
        context.PublishedStockLow().Should().BeEmpty();
    }

    [Fact]
    public async Task Decrement_staying_above_threshold_does_not_publish_low_stock()
    {
        await using var context = await ConsumerTestContext.Create();

        await context.Publish(productId: 1, quantity: 5);

        (await context.StockOf(1)).Should().Be(45);
        context.PublishedStockLow().Should().BeEmpty();
    }

    [Fact]
    public async Task Low_stock_is_level_triggered_and_publishes_on_every_decrement_below_threshold()
    {
        await using var context = await ConsumerTestContext.Create();

        await context.Publish(productId: 6, quantity: 1); // product 6 starts at 8, already low
        await context.Publish(productId: 6, quantity: 1);

        (await context.StockOf(6)).Should().Be(6);
        context.PublishedStockLow().Should().HaveCount(2);
    }

    [Fact]
    public async Task Unknown_product_is_recorded_and_leaves_stock_untouched()
    {
        await using var context = await ConsumerTestContext.Create();

        await context.Publish(productId: 999, quantity: 1);

        (await context.ProcessedMessageCount()).Should().Be(1);
        (await context.StockOf(1)).Should().Be(50);
        context.PublishedStockLow().Should().BeEmpty();
    }
}
