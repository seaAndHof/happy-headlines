using System.Text.Json;
using Dapper;
using HappyHeadlines.Shared;
using Npgsql;
using StackExchange.Redis;

namespace ArticleCacheWarmer;

public class ArticleCacheRefresher(
    NpgsqlDataSource database, IConnectionMultiplexer cache, IConfiguration config, ILogger<ArticleCacheRefresher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = config.GetValue<TimeSpan>("RefreshInterval");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(entryLifetime: interval * 2);
            }
            catch (Exception e)
            {
                logger.LogError(e, "ArticleCache refresh failed; the cache keeps its current entries until they expire");
            }
            await Task.Delay(interval, stoppingToken);
        }
    }
     
    private async Task RefreshAsync(TimeSpan entryLifetime)
    {
        await using var connection = await database.OpenConnectionAsync();
        var articles = (await connection.QueryAsync<Article>(
            """
            SELECT id, title, content, published_at AS PublishedAt FROM articles
            WHERE published_at >= now() - interval '14 days'
            """)).ToList();

        var redis = cache.GetDatabase();
        foreach (var article in articles)
            await redis.StringSetAsync(CacheKeys.Article(article.Id), JsonSerializer.Serialize(article), entryLifetime);

        logger.LogInformation("ArticleCache refreshed with {Count} articles from the last 14 days", articles.Count);
    }
}
