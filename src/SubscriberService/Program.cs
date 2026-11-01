using Dapper;
using HappyHeadlines.Shared;
using MassTransit;
using Microsoft.FeatureManagement;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("SubscriberService");
builder.AddFeatureToggles();
var config = builder.Configuration;

builder.Services.AddSingleton(NpgsqlDataSource.Create(config["SubscriberDatabase"]!));
builder.Services.AddMassTransit(x => x.UsingRabbitMq((_, cfg) =>
    cfg.Host(config["RabbitMq"], "/", h => { h.Username("guest"); h.Password("guest"); })));

var app = builder.Build();

// Release toggle: while SubscriberService is switched off, the whole service answers 404, as if it didn't exist.
var subscribers = app.MapGroup("/subscribers").AddEndpointFilter(async (context, next) =>
    await context.HttpContext.RequestServices.GetRequiredService<IFeatureManager>().IsEnabledAsync(FeatureToggles.SubscriberService)
        ? await next(context)
        : Results.NotFound("SubscriberService is switched off"));

subscribers.MapGet("/", async (NpgsqlDataSource database) =>
{
    await using var connection = await database.OpenConnectionAsync();
    return await connection.QueryAsync<Subscriber>("SELECT email, subscribed_at AS SubscribedAt FROM subscribers ORDER BY email");
});

subscribers.MapPost("/", async (NewSubscriber input, NpgsqlDataSource database, IPublishEndpoint queue) =>
{
    await using var connection = await database.OpenConnectionAsync();
    var subscriber = await connection.QuerySingleOrDefaultAsync<Subscriber>(
        """
        INSERT INTO subscribers (email) VALUES (@Email) ON CONFLICT (email) DO NOTHING
        RETURNING email, subscribed_at AS SubscribedAt
        """, input);
    if (subscriber is null)
        return Results.Conflict($"{input.Email} is already subscribed");

    // New subscribers go into the SubscriberQueue; NewsletterService picks them up and sends a welcome mail.
    await queue.Publish(new SubscriberAdded(subscriber.Email));
    return Results.Created($"/subscribers/{subscriber.Email}", subscriber);
});

subscribers.MapDelete("/{email}", async (string email, NpgsqlDataSource database) =>
{
    await using var connection = await database.OpenConnectionAsync();
    var deleted = await connection.ExecuteAsync("DELETE FROM subscribers WHERE email = @email", new { email });
    return deleted == 0 ? Results.NotFound() : Results.NoContent();
});

app.Run();
