using System.Diagnostics;
using Dapper;
using HappyHeadlines.Shared;
using MassTransit;
using Npgsql;

namespace CommentService;

public class GetCommentsConsumer(NpgsqlDataSource database, CommentCache cache) : IConsumer<GetComments>
{
    public async Task Consume(ConsumeContext<GetComments> context)
    {
        var articleId = context.Message.ArticleId;
        var timer = Stopwatch.StartNew();

        var cached = await cache.GetAsync(articleId);
        if (cached != null)
        {
            CacheMetrics.Record(CacheMetrics.Comment, hit: true, timer.Elapsed);
            await context.RespondAsync(new CommentsResponse(cached, CacheHit: true));
            return;
        }

        List<Comment> comments;
        try
        {
            await using var connection = await database.OpenConnectionAsync();
            comments = (await connection.QueryAsync<Comment>(
                """
                SELECT id, article_id AS ArticleId, author, body, created_at AS CreatedAt FROM comments
                WHERE article_id = @articleId ORDER BY created_at
                """,
                new { articleId })).ToList();
        }
        finally
        {
            CacheMetrics.Record(CacheMetrics.Comment, hit: false, timer.Elapsed);
        }
        await cache.AddAsync(articleId, comments);
        await context.RespondAsync(new CommentsResponse(comments, CacheHit: false));
    }
}
