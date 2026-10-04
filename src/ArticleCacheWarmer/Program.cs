using ArticleCacheWarmer;
using Npgsql;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;

builder.Services.AddSingleton(NpgsqlDataSource.Create(config["ArticleDatabase"]!));
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(config["ArticleCache"]!));
builder.Services.AddHostedService<ArticleCacheRefresher>();

builder.Build().Run();
