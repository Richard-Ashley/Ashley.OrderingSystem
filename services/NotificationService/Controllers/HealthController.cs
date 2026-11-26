using Microsoft.AspNetCore.Mvc;

namespace NotificationService.Controllers;

/// <summary>Health check endpoint for the event-only NotificationService.</summary>
[ApiController]
[Route("api/health")]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Returns 200 if the service is up. Used by docker-compose health checks and load balancers.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() =>
        Ok(new HealthResponse("healthy", DateTimeOffset.UtcNow));
}

public sealed record HealthResponse(string Status, DateTimeOffset CheckedAt);
