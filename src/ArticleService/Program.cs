using ArticleService;
using MassTransit;
using Npgsql;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddSingleton(NpgsqlDataSource.Create(config["ArticleDatabase"]!));
var cacheOptions = ConfigurationOptions.Parse(config["ArticleCache"]!);
cacheOptions.BacklogPolicy = BacklogPolicy.FailFast;
cacheOptions.AsyncTimeout = 250;
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(cacheOptions));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<GetArticleConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(config["RabbitMq"], "/", h => { h.Username("guest"); h.Password("guest"); });
        cfg.ReceiveEndpoint("article-service", e => e.ConfigureConsumer<GetArticleConsumer>(context));
    });
});

var app = builder.Build();
app.MapMetrics(); 
app.Run();
