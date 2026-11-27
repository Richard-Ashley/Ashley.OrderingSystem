using Microsoft.AspNetCore.Http;

namespace Observability;

public static class Correlation
{
    /// <summary>HTTP header carrying the correlation id between services.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Serilog property that every log line in the request or message scope carries.</summary>
    public const string LogProperty = "CorrelationId";

    internal const string ItemKey = "Observability.CorrelationId";
}

/// <summary>Gives code inside an HTTP request access to the current correlation id.</summary>
public interface ICorrelationIdAccessor
{
    /// <summary>The correlation id of the current request, or a new one if there is none.</summary>
    Guid Current { get; }
}

internal sealed class HttpCorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IHttpContextAccessor _httpContext;

    public HttpCorrelationIdAccessor(IHttpContextAccessor httpContext)
    {
        _httpContext = httpContext;
    }

    public Guid Current =>
        _httpContext.HttpContext?.Items[Correlation.ItemKey] is Guid id ? id : Guid.NewGuid();
}
