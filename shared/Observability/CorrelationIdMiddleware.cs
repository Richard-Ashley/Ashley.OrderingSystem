using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Observability;

/// <summary>
/// Reads X-Correlation-Id (or generates one), stores it for the request, echoes it on the
/// response and pushes it into the log scope so every log line in the request carries it.
/// Inbound values that are not GUIDs are replaced, because MassTransit correlation ids are GUIDs.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            Guid.TryParse(context.Request.Headers[Correlation.HeaderName].FirstOrDefault(), out var inbound)
                ? inbound
                : Guid.NewGuid();

        context.Items[Correlation.ItemKey] = correlationId;
        context.Response.Headers[Correlation.HeaderName] = correlationId.ToString();

        using (LogContext.PushProperty(Correlation.LogProperty, correlationId))
        {
            await _next(context);
        }
    }
}
