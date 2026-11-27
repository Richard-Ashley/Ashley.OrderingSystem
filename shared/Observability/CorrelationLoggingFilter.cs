using MassTransit;
using SerilogLogContext = Serilog.Context.LogContext;

namespace Observability;

/// <summary>
/// MassTransit consume filter that puts the message's correlation id into the log scope,
/// so every log line written while a consumer handles a message carries it.
/// </summary>
public sealed class CorrelationLoggingFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var correlationId = context.CorrelationId ?? context.ConversationId;

        using (SerilogLogContext.PushProperty(Correlation.LogProperty, correlationId))
        {
            await next.Send(context);
        }
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("correlationLogging");
}
