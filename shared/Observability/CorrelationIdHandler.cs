namespace Observability;

/// <summary>Adds the current request's correlation id to outbound HttpClient calls.</summary>
public sealed class CorrelationIdHandler : DelegatingHandler
{
    private readonly ICorrelationIdAccessor _correlation;

    public CorrelationIdHandler(ICorrelationIdAccessor correlation)
    {
        _correlation = correlation;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Remove(Correlation.HeaderName);
        request.Headers.Add(Correlation.HeaderName, _correlation.Current.ToString());
        return base.SendAsync(request, cancellationToken);
    }
}
