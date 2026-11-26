using Contracts.Inventory;
using FluentValidation;
using InventoryService.Models;
using InventoryService.Services;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/inventory")]
[Produces("application/json")]
public sealed class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventory;
    private readonly IValidator<StockCheckRequest> _stockCheckValidator;

    public InventoryController(
        IInventoryService inventory,
        IValidator<StockCheckRequest> stockCheckValidator)
    {
        _inventory = inventory;
        _stockCheckValidator = stockCheckValidator;
    }

    /// <summary>
    /// Checks whether the given quantity of a product is in stock.
    /// Called by OrdersService during order placement.
    /// </summary>
    [HttpPost("check")]
    [ProducesResponseType(typeof(StockCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockCheckResponse>> CheckStock(
        [FromBody] StockCheckRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _stockCheckValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var response = await _inventory.CheckStock(request, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>Returns a product's current state.</summary>
    [HttpGet("{productId:int}")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Product>> GetProduct(int productId, CancellationToken cancellationToken)
    {
        var product = await _inventory.GetProduct(productId, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>Lists all products with their current stock levels. Useful for browsing in Swagger.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Product>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Product>>> ListProducts(CancellationToken cancellationToken)
    {
        var products = await _inventory.ListProducts(cancellationToken);
        return Ok(products);
    }
}
