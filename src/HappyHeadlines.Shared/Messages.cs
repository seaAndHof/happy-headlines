namespace HappyHeadlines.Shared;

public record Article(int Id, string Title, string Content, DateTime PublishedAt);

public record Comment(int Id, int ArticleId, string Author, string Body, DateTime CreatedAt);

public record NewComment(int ArticleId, string Author, string Body);

public record FilterRequest(string Text);
public record FilterResponse(string Text);

public record Subscriber(string Email, DateTime SubscribedAt);
public record NewSubscriber(string Email);

/// <summary>Put into the SubscriberQueue when someone subscribes.</summary>
public record SubscriberAdded(string Email);
