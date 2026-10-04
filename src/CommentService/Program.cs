using CommentService;
using MassTransit;
using Npgsql;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddSingleton(NpgsqlDataSource.Create(config["CommentDatabase"]!));
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(config["CommentCache"]!));
builder.Services.AddSingleton<CommentCache>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<GetCommentsConsumer>();
    x.AddConsumer<PostCommentConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(config["RabbitMq"], "/", h => { h.Username("guest"); h.Password("guest"); });
        cfg.ReceiveEndpoint("comment-service", e =>
        {
            e.ConfigureConsumer<GetCommentsConsumer>(context);
            e.ConfigureConsumer<PostCommentConsumer>(context);
        });
    });
});

var app = builder.Build();
app.MapMetrics(); 
app.Run();
