using System.Net.Http.Json;
using HappyHeadlines.Shared;

namespace ReaderSimulator;

public class Readers(IHttpClientFactory http, IConfiguration config, ILogger<Readers> logger) : BackgroundService
{
    private static readonly string[] CommentBodies =
        ["What a lovely story!", "This made my day.", "Damn, that is good news!", "Only an idiot could dislike this."];

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, config.GetValue<int>("Readers")).Select(_ => ReadAsync(stoppingToken)));

    private async Task ReadAsync(CancellationToken stoppingToken)
    {
        var articles = http.CreateClient("ArticleService");
        var comments = http.CreateClient("CommentService");

        while (!stoppingToken.IsCancellationRequested)
        {
            // 80% of readers open news from the last 14 days, 20% open an older article.
            var articleId = Random.Shared.NextDouble() < 0.8 ? Random.Shared.Next(1, 68) : Random.Shared.Next(68, 301);
            try
            {
                (await articles.GetAsync($"articles/global/{articleId}", stoppingToken)).EnsureSuccessStatusCode();
                (await comments.GetAsync($"comments/{articleId}", stoppingToken)).EnsureSuccessStatusCode();

                if (Random.Shared.NextDouble() < 0.05)
                {
                    var body = CommentBodies[Random.Shared.Next(CommentBodies.Length)];
                    (await comments.PostAsJsonAsync("comments", new NewComment(articleId, "Reader", body), stoppingToken))
                        .EnsureSuccessStatusCode();
                }
            }
            catch (HttpRequestException e)
            {
                logger.LogWarning("Reading article {ArticleId} failed: {Reason}", articleId, e.Message);
            }

            await Task.Delay(200, stoppingToken);
        }
    }
}
