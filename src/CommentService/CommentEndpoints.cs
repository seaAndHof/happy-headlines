using System.Diagnostics;
using Dapper;
using HappyHeadlines.Shared;
using Npgsql;

namespace CommentService;

public static class CommentEndpoints
{
    private const string Columns = "id, article_id AS ArticleId, author, body, created_at AS CreatedAt";

    public static void MapCommentEndpoints(this WebApplication app)
    {
        app.MapGet("/comments/{articleId:int}", Get);
        app.MapPost("/comments", Post);
    }

    // The CommentCache is filled on a miss and holds the comments of the 30 most recently read articles (compulsory 1).
    private static async Task<IResult> Get(int articleId, NpgsqlDataSource database, CommentCache cache, HttpResponse response)
    {
        var timer = Stopwatch.StartNew();

        var cached = await cache.GetAsync(articleId);
        if (cached != null)
        {
            CacheMetrics.Record(CacheMetrics.Comment, hit: true, timer.Elapsed);
            response.Headers["X-Cache"] = "hit";
            return Results.Ok(cached);
        }

        List<Comment> comments;
        try
        {
            await using var connection = await database.OpenConnectionAsync();
            comments = (await connection.QueryAsync<Comment>(
                $"SELECT {Columns} FROM comments WHERE article_id = @articleId ORDER BY created_at", new { articleId })).ToList();
        }
        finally
        {
            CacheMetrics.Record(CacheMetrics.Comment, hit: false, timer.Elapsed);
        }
        await cache.AddAsync(articleId, comments);
        response.Headers["X-Cache"] = "miss";
        return Results.Ok(comments);
    }

    private static async Task<IResult> Post(
        NewComment input, NpgsqlDataSource database, CommentCache cache, ProfanityClient profanity, CancellationToken cancellationToken)
    {
        var body = await profanity.FilterAsync(input.Body, cancellationToken);

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var comment = await connection.QuerySingleAsync<Comment>(
            $"INSERT INTO comments (article_id, author, body) VALUES (@ArticleId, @Author, @Body) RETURNING {Columns}",
            new { input.ArticleId, input.Author, Body = body });

        await cache.RemoveAsync(input.ArticleId); // the next read loads the list with the new comment
        return Results.Created($"/comments/{comment.ArticleId}", comment);
    }
}
