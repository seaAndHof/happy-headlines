namespace HappyHeadlines.Shared;

public record Article(int Id, string Title, string Content, DateTime PublishedAt);

public record Comment(int Id, int ArticleId, string Author, string Body, DateTime CreatedAt);

public record GetComments(int ArticleId);
public record CommentsResponse(List<Comment> Comments, bool CacheHit);

public record PostComment(int ArticleId, string Author, string Body);
