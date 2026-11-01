using HappyHeadlines.Shared;
using MassTransit;
using NewsletterService;

var builder = Host.CreateApplicationBuilder(args);
builder.AddObservability("NewsletterService");
builder.AddFeatureToggles();
var config = builder.Configuration;

// Calls to other swimlanes get timeouts, retries and a circuit breaker, so a slow or failing service can't hang us.
builder.Services.AddHttpClient("ArticleService", c => c.BaseAddress = new Uri(config["ArticleService"]!))
    .AddStandardResilienceHandler();
builder.Services.AddHttpClient("SubscriberService", c => c.BaseAddress = new Uri(config["SubscriberService"]!))
    .AddStandardResilienceHandler();
builder.Services.AddHostedService<NewsletterSender>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<WelcomeMailConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(config["RabbitMq"], "/", h => { h.Username("guest"); h.Password("guest"); });
        cfg.ReceiveEndpoint("subscriber-queue", e => e.ConfigureConsumer<WelcomeMailConsumer>(context));
    });
});

builder.Build().Run();
