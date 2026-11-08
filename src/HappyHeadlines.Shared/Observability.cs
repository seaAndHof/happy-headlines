using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace HappyHeadlines.Shared;

/// <summary>
/// Design to be monitored: every service logs and traces the same way, to one central place each.
/// Logs go to Seq, traces go to Jaeger, and metrics are scraped by Prometheus from /metrics.
/// </summary>
public static class Observability
{
    public static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder, string serviceName)
    {
        var seq = builder.Configuration["Seq"] ?? "http://seq:5341";

        // GAF tactic "Reduce Logging": the framework logged every request several times over (ASP.NET Core hosting,
        // routing and results; HttpClient handlers). Jaeger already records every request as a trace, so the framework
        // only logs warnings and errors; our own Information logs stay.
        builder.Services.AddSerilog(logging => logging
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console()
            .WriteTo.Seq(seq));

        // Traces cross service boundaries: the W3C traceparent header travels with every HTTP call (and every
        // RabbitMQ message), so one request through several services becomes one trace. The exporter's endpoint
        // comes from OTEL_EXPORTER_OTLP_ENDPOINT.
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(o => o.Filter = context => context.Request.Path != "/metrics")
                .AddHttpClientInstrumentation(o => o.FilterHttpRequestMessage = request =>
                    request.RequestUri?.Authority != new Uri(seq).Authority) // not the log shipping to Seq
                .AddNpgsql()
                .AddSource("MassTransit")
                .AddOtlpExporter());

        return builder;
    }
}
