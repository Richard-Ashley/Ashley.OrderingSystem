using Contracts.Inventory;
using FluentAssertions;
using InventoryService.Validators;

namespace OrderingSystem.Tests.Inventory;

public sealed class StockCheckRequestValidatorTests
{
    private readonly StockCheckRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        _validator.Validate(new StockCheckRequest(ProductId: 1, Quantity: 5)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_product_id_is_rejected(int productId)
    {
        var result = _validator.Validate(new StockCheckRequest(productId, Quantity: 1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "ProductId")
            .Which.ErrorMessage.Should().Be("Product id must be positive.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_quantity_is_rejected(int quantity)
    {
        var result = _validator.Validate(new StockCheckRequest(ProductId: 1, quantity));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Quantity")
            .Which.ErrorMessage.Should().Be("Quantity must be positive.");
    }
}
