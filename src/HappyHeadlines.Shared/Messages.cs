namespace HappyHeadlines.Shared;

public record Article(int Id, string Title, string Content, DateTime PublishedAt);

public record Comment(int Id, int ArticleId, string Author, string Body, DateTime CreatedAt);

public record NewComment(int ArticleId, string Author, string Body);

public record FilterRequest(string Text);
public record FilterResponse(string Text);
