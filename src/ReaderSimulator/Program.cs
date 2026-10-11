using HappyHeadlines.Shared;
using ReaderSimulator;

var builder = Host.CreateApplicationBuilder(args);
builder.AddObservability("ReaderSimulator");
var config = builder.Configuration;

// ArticleService through its load balancer, like the Website would; CommentService directly.
builder.Services.AddHttpClient("ArticleService", c => c.BaseAddress = new Uri(config["ArticleService"]!));
builder.Services.AddHttpClient("CommentService", c => c.BaseAddress = new Uri(config["CommentService"]!));
builder.Services.AddHostedService<Readers>();

builder.Build().Run();
