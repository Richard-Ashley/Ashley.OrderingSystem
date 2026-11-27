using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Observability;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Configures Serilog (JSON on the console, plus Seq when Seq:ServerUrl is set) and the
    /// services needed to propagate correlation ids.
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((context, services, logger) =>
        {
            logger
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", serviceName)
                .WriteTo.Console(new RenderedCompactJsonFormatter());

            var seqUrl = context.Configuration["Seq:ServerUrl"];
            if (!string.IsNullOrWhiteSpace(seqUrl))
            {
                logger.WriteTo.Seq(seqUrl);
            }
        });

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ICorrelationIdAccessor, HttpCorrelationIdAccessor>();
        builder.Services.AddTransient<CorrelationIdHandler>();

        return builder;
    }

    /// <summary>Adds correlation id handling and one structured log line per HTTP request.</summary>
    public static WebApplication UseObservability(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging();
        return app;
    }

    /// <summary>
    /// Applies the correlation logging filter to every consumer on the bus and prefixes queue
    /// names with the service name, so each service gets its own copy of a published event
    /// instead of competing with other services for one shared queue.
    /// </summary>
    public static void UseObservability<TEndpoint>(
        this IBusFactoryConfigurator<TEndpoint> cfg,
        IBusRegistrationContext context,
        string serviceName)
        where TEndpoint : IReceiveEndpointConfigurator
    {
        cfg.UseConsumeFilter(typeof(CorrelationLoggingFilter<>), context);
        cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter(serviceName, includeNamespace: false));
    }
}
