using Contracts.Inventory;
using FluentValidation;

namespace InventoryService.Validators;

public sealed class StockCheckRequestValidator : AbstractValidator<StockCheckRequest>
{
    public StockCheckRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0).WithMessage("Product id must be positive.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be positive.");
    }
}
