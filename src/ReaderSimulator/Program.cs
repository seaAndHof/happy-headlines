using MassTransit;
using ReaderSimulator;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
        cfg.Host(config["RabbitMq"], "/", h => { h.Username("guest"); h.Password("guest"); }));
});
builder.Services.AddHostedService<Readers>();

builder.Build().Run();
