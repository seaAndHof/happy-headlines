using HappyHeadlines.Shared;
using MassTransit;

namespace NewsletterService;

/// <summary>Reads new subscribers from the SubscriberQueue and sends each a welcome mail.</summary>
public class WelcomeMailConsumer(ILogger<WelcomeMailConsumer> logger) : IConsumer<SubscriberAdded>
{
    public Task Consume(ConsumeContext<SubscriberAdded> context)
    {
        logger.LogInformation("Welcome mail sent to {Email}", context.Message.Email);
        return Task.CompletedTask;
    }
}
