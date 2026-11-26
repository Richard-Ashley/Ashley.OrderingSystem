using Contracts.Inventory;
using FluentValidation;

namespace InventoryService.Validators;

public sealed class StockCheckRequestValidator : AbstractValidator<StockCheckRequest>
{
    public StockCheckRequestValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
