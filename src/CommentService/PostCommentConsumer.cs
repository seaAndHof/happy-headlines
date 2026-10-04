using Dapper;
using HappyHeadlines.Shared;
using MassTransit;
using Npgsql;

namespace CommentService;

public class PostCommentConsumer(NpgsqlDataSource database, CommentCache cache) : IConsumer<PostComment>
{
    public async Task Consume(ConsumeContext<PostComment> context)
    {
        await using var connection = await database.OpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO comments (article_id, author, body) VALUES (@ArticleId, @Author, @Body)", context.Message);

        await cache.RemoveAsync(context.Message.ArticleId);
    }
}
