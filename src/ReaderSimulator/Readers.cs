using HappyHeadlines.Shared;
using MassTransit;

namespace ReaderSimulator;

public class Readers(IBus bus, IHttpClientFactory http, IConfiguration config, ILogger<Readers> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, config.GetValue<int>("Readers")).Select(_ => ReadAsync(stoppingToken)));

    private async Task ReadAsync(CancellationToken stoppingToken)
    {
        var articles = http.CreateClient("ArticleService");
        var comments = bus.CreateRequestClient<GetComments>();

        while (!stoppingToken.IsCancellationRequested)
        {
            // 80% of readers open news from the last 14 days, 20% open an older article.
            var articleId = Random.Shared.NextDouble() < 0.8 ? Random.Shared.Next(1, 68) : Random.Shared.Next(68, 301);
            try
            {
                (await articles.GetAsync($"articles/global/{articleId}", stoppingToken)).EnsureSuccessStatusCode();
                await comments.GetResponse<CommentsResponse>(new GetComments(articleId), stoppingToken);

                if (Random.Shared.NextDouble() < 0.05)
                    await bus.Publish(new PostComment(articleId, "Reader", "What a lovely story!"), stoppingToken);
            }
            catch (Exception e) when (e is HttpRequestException or RequestTimeoutException or RequestFaultException)
            {
                logger.LogWarning("Reading article {ArticleId} failed: {Reason}", articleId, e.Message);
            }

            await Task.Delay(200, stoppingToken);
        }
    }
}
