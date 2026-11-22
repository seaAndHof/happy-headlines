using System.Diagnostics;
using System.Text.Json;
using Dapper;
using HappyHeadlines.Shared;
using Npgsql;
using StackExchange.Redis;

namespace ArticleService;

/// <summary>REST CRUD for articles. Every route names its continent, which picks the database (z-axis).</summary>
public static class ArticleEndpoints
{
    private const string Columns = "id, title, content, published_at AS PublishedAt";

    public static void MapArticleEndpoints(this WebApplication app)
    {
        var articles = app.MapGroup("/articles/{continent}");
        articles.MapGet("/", GetRecent);
        articles.MapGet("/{id:int}", Get);
        articles.MapPost("/", Create);
        articles.MapPut("/{id:int}", Update);
        articles.MapDelete("/{id:int}", Delete);
    }

    private static async Task<IResult> GetRecent(string continent, ArticleDatabaseCoordinator coordinator, int days = 1)
    {
        if (coordinator.GetDatabase(continent) is not { } database)
            return Results.NotFound($"Unknown continent '{continent}'");

        await using var connection = await database.OpenConnectionAsync();
        var articles = await connection.QueryAsync<Article>(
            $"SELECT {Columns} FROM articles WHERE published_at >= now() - make_interval(days => @days) ORDER BY published_at DESC",
            new { days });
        return Results.Ok(articles);
    }

    private static async Task<IResult> Get(
        string continent, int id, ArticleDatabaseCoordinator coordinator, IConnectionMultiplexer cache, HttpResponse response)
    {
        if (coordinator.GetDatabase(continent) is not { } database)
            return Results.NotFound($"Unknown continent '{continent}'");

        if (continent != ArticleDatabaseCoordinator.Global)
            return await ReadAsync(database, id) is { } article ? Results.Ok(article) : Results.NotFound();

        // Global articles: the ArticleCache sits between this service and the global database (compulsory 1).
        // A hit is answered from the cache; a miss reads the database but does not fill the cache, since only the
        // offline ArticleCacheWarmer does that.
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
            response.Headers["X-Cache"] = "hit";
            return Results.Ok(JsonSerializer.Deserialize<Article>(cached.ToString()));
        }

        Article? fromDatabase;
        try
        {
            fromDatabase = await ReadAsync(database, id);
        }
        finally
        {
            CacheMetrics.Record(CacheMetrics.Article, hit: false, timer.Elapsed);
        }
        response.Headers["X-Cache"] = "miss";
        return fromDatabase is null ? Results.NotFound() : Results.Ok(fromDatabase);
    }

    private static async Task<IResult> Create(string continent, ArticleInput input, ArticleDatabaseCoordinator coordinator)
    {
        if (coordinator.GetDatabase(continent) is not { } database)
            return Results.NotFound($"Unknown continent '{continent}'");

        await using var connection = await database.OpenConnectionAsync();
        var article = await connection.QuerySingleAsync<Article>(
            $"INSERT INTO articles (title, content, published_at) VALUES (@Title, @Content, now()) RETURNING {Columns}",
            input);
        return Results.Created($"/articles/{continent}/{article.Id}", article);
    }

    private static async Task<IResult> Update(
        string continent, int id, ArticleInput input, ArticleDatabaseCoordinator coordinator, IConnectionMultiplexer cache)
    {
        if (coordinator.GetDatabase(continent) is not { } database)
            return Results.NotFound($"Unknown continent '{continent}'");

        await using var connection = await database.OpenConnectionAsync();
        var article = await connection.QuerySingleOrDefaultAsync<Article>(
            $"UPDATE articles SET title = @Title, content = @Content WHERE id = @id RETURNING {Columns}",
            new { input.Title, input.Content, id });
        await ForgetCachedAsync(continent, id, cache);
        return article is null ? Results.NotFound() : Results.Ok(article);
    }

    private static async Task<IResult> Delete(
        string continent, int id, ArticleDatabaseCoordinator coordinator, IConnectionMultiplexer cache)
    {
        if (coordinator.GetDatabase(continent) is not { } database)
            return Results.NotFound($"Unknown continent '{continent}'");

        await using var connection = await database.OpenConnectionAsync();
        var deleted = await connection.ExecuteAsync("DELETE FROM articles WHERE id = @id", new { id });
        await ForgetCachedAsync(continent, id, cache);
        return deleted == 0 ? Results.NotFound() : Results.NoContent();
    }

    private static async Task<Article?> ReadAsync(NpgsqlDataSource database, int id)
    {
        await using var connection = await database.OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<Article>($"SELECT {Columns} FROM articles WHERE id = @id", new { id });
    }

    // An edited or deleted global article must not be served stale from the ArticleCache until it expires.
    private static async Task ForgetCachedAsync(string continent, int id, IConnectionMultiplexer cache)
    {
        if (continent == ArticleDatabaseCoordinator.Global)
            await cache.GetDatabase().KeyDeleteAsync(CacheKeys.Article(id));
    }
}

public record ArticleInput(string Title, string Content);
