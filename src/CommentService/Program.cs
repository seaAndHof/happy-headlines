using CommentService;
using HappyHeadlines.Shared;
using Microsoft.Extensions.Http.Resilience;
using Npgsql;
using Polly;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("CommentService");
var config = builder.Configuration;

builder.Services.AddSingleton(NpgsqlDataSource.Create(config["CommentDatabase"]!));
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(config["CommentCache"]!));
builder.Services.AddSingleton<CommentCache>();

// CommentService calls ProfanityService directly. The circuit breaker keeps a ProfanityService failure from
// spreading into CommentService: after repeated failures it stops calling for a while and the fallback takes over.
builder.Services.AddHttpClient<ProfanityClient>(c => c.BaseAddress = new Uri(config["ProfanityService"]!))
    .AddResilienceHandler("profanity", (pipeline, context) =>
    {
        var logger = context.ServiceProvider.GetRequiredService<ILogger<ProfanityClient>>();
        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,                          // open when half the calls fail...
            MinimumThroughput = 5,                       // ...out of at least 5 calls...
            SamplingDuration = TimeSpan.FromSeconds(10), // ...within 10 seconds,
            BreakDuration = TimeSpan.FromSeconds(15),    // then stay open for 15 seconds before trying again (half-open).
            OnOpened = args =>
            {
                logger.LogWarning("Circuit to ProfanityService opened for {BreakDuration}", args.BreakDuration);
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = _ =>
            {
                logger.LogInformation("Circuit to ProfanityService half-open: trying one call");
                return ValueTask.CompletedTask;
            },
            OnClosed = _ =>
            {
                logger.LogInformation("Circuit to ProfanityService closed again");
                return ValueTask.CompletedTask;
            },
        });
        pipeline.AddTimeout(TimeSpan.FromSeconds(1)); // a slow answer counts as a failure too
    });

var app = builder.Build();
app.MapCommentEndpoints();
app.MapMetrics();
app.Run();
