using System.Diagnostics;
using System.Text.Json;
using Dapper;
using HappyHeadlines.Shared;
using MassTransit;
using Npgsql;
using StackExchange.Redis;

namespace ArticleService;

public class GetArticleConsumer(NpgsqlDataSource database, IConnectionMultiplexer cache) : IConsumer<GetArticle>
{
    public async Task Consume(ConsumeContext<GetArticle> context)
    {
        var id = context.Message.ArticleId;
        var timer = Stopwatch.StartNew();

        var cached = RedisValue.Null;
        try
        {
            cached = await cache.GetDatabase().StringGetAsync(CacheKeys.Article(id));
        }
        catch (Exception)
        {
            // If Redis is down or slow, we still want to serve the article from the database.
        }
        if (cached.HasValue)
        {
            CacheMetrics.Record(CacheMetrics.Article, hit: true, timer.Elapsed);
            await context.RespondAsync(new ArticleResponse(JsonSerializer.Deserialize<Article>(cached.ToString()), CacheHit: true));
            return;
        }

        Article? article;
        try
        {
            await using var connection = await database.OpenConnectionAsync();
            article = await connection.QuerySingleOrDefaultAsync<Article>(
                "SELECT id, title, content, published_at AS PublishedAt FROM articles WHERE id = @id", new { id });
        }
        finally
        {
            CacheMetrics.Record(CacheMetrics.Article, hit: false, timer.Elapsed);
        }
        await context.RespondAsync(new ArticleResponse(article, CacheHit: false));
    }
}
