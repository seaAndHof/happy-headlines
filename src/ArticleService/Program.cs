using ArticleService;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddSingleton<ArticleDatabaseCoordinator>();
var cacheOptions = ConfigurationOptions.Parse(config["ArticleCache"]!);
cacheOptions.BacklogPolicy = BacklogPolicy.FailFast;
cacheOptions.AsyncTimeout = 250;
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(cacheOptions));

var app = builder.Build();
app.MapArticleEndpoints();
app.MapMetrics();
app.Run();
