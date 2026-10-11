using ArticleCacheWarmer;
using HappyHeadlines.Shared;
using Npgsql;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.AddObservability("ArticleCacheWarmer");
var config = builder.Configuration;

builder.Services.AddSingleton(NpgsqlDataSource.Create(config["ArticleDatabase"]!));
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(config["ArticleCache"]!));
builder.Services.AddHostedService<ArticleCacheRefresher>();

builder.Build().Run();
