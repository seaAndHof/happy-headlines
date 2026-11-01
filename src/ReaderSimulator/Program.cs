using HappyHeadlines.Shared;
using ReaderSimulator;

var builder = Host.CreateApplicationBuilder(args);
builder.AddObservability("ReaderSimulator");
var config = builder.Configuration;

// ArticleService through its load balancer, like the Website would; the others directly.
builder.Services.AddHttpClient("ArticleService", c => c.BaseAddress = new Uri(config["ArticleService"]!));
builder.Services.AddHttpClient("CommentService", c => c.BaseAddress = new Uri(config["CommentService"]!));
builder.Services.AddHttpClient("SubscriberService", c => c.BaseAddress = new Uri(config["SubscriberService"]!));
builder.Services.AddHostedService<Readers>();

builder.Build().Run();
