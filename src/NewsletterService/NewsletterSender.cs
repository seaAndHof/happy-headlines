using System.Net.Http.Json;
using HappyHeadlines.Shared;
using Microsoft.FeatureManagement;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace NewsletterService;

/// <summary>Sends the newsletter: the latest global articles to every subscriber.</summary>
public class NewsletterSender(
    IFeatureManager features, IHttpClientFactory http, IConfiguration config, ILogger<NewsletterSender> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = config.GetValue<TimeSpan>("NewsletterInterval");
        while (!stoppingToken.IsCancellationRequested)
        {
            await SendAsync(stoppingToken);
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task SendAsync(CancellationToken cancellationToken)
    {
        // Release toggle: SubscriberService can be switched off without redeploying. While it's off,
        // NewsletterService doesn't call it at all, and no newsletter goes out.
        if (!await features.IsEnabledAsync(FeatureToggles.SubscriberService))
        {
            logger.LogInformation("SubscriberService is switched off: no newsletter this time");
            return;
        }

        try
        {
            var articles = await http.CreateClient("ArticleService")
                .GetFromJsonAsync<List<Article>>("articles/global?days=1", cancellationToken);
            var subscribers = await http.CreateClient("SubscriberService")
                .GetFromJsonAsync<List<Subscriber>>("subscribers", cancellationToken);

            logger.LogInformation("Newsletter with {ArticleCount} articles sent to {SubscriberCount} subscribers",
                articles!.Count, subscribers!.Count);
        }
        catch (Exception e) when (e is HttpRequestException or BrokenCircuitException or TimeoutRejectedException)
        {
            // Fault isolation: if SubscriberService (or ArticleService) is down, only this newsletter is skipped.
            // NewsletterService keeps running, and welcome mails keep flowing from the SubscriberQueue.
            logger.LogWarning("Newsletter skipped: {Reason}", e.Message);
        }
    }
}
