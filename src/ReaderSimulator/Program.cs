using MassTransit;
using ReaderSimulator;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
        cfg.Host(config["RabbitMq"], "/", h => { h.Username("guest"); h.Password("guest"); }));
});
// Through the load balancer, like the Website would.
builder.Services.AddHttpClient("ArticleService", c => c.BaseAddress = new Uri(config["ArticleService"]!));
builder.Services.AddHostedService<Readers>();

builder.Build().Run();
