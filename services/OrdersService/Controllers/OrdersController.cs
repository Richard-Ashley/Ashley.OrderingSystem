using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrdersService.Data;
using OrdersService.Models;
using OrdersService.Services;

namespace OrdersService.Controllers;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IValidator<PlaceOrderRequest> _validator;

    public OrdersController(IOrderService orderService, IValidator<PlaceOrderRequest> validator)
    {
        _orderService = orderService;
        _validator = validator;
    }

    /// <summary>Place a new order after checking stock with InventoryService.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PlaceOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PlaceOrderResponse>> Place(
        [FromBody] PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var result = await _orderService.PlaceOrder(request, cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new ProblemDetails
            {
                Title = result.StatusCode == StatusCodes.Status503ServiceUnavailable
                    ? "Inventory service unavailable"
                    : "Order could not be placed",
                Detail = result.Reason,
                Status = result.StatusCode
            });
        }

        var response = new PlaceOrderResponse(result.OrderId!.Value, OrderStatus.Placed.ToString());
        return CreatedAtAction(nameof(Get), new { id = result.OrderId }, response);
    }

    /// <summary>Returns an order by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Order>> Get(
        Guid id,
        [FromServices] OrdersDbContext db,
        CancellationToken cancellationToken)
    {
        var order = await db.Orders.FindAsync(new object?[] { id }, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }
}
