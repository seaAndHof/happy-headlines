using System.Text.Json;
using HappyHeadlines.Shared;
using StackExchange.Redis;

namespace CommentService;


public class CommentCache(IConnectionMultiplexer redis, IConfiguration config)
{
    private const string LastRead = "comments:last-read";
    private readonly IDatabase _cache = redis.GetDatabase();
    private readonly int _maxArticles = config.GetValue<int>("CommentCacheMaxArticles");

    private static string Key(int articleId) => $"comments:{articleId}";

    public async Task<List<Comment>?> GetAsync(int articleId)
    {
        var cached = await _cache.StringGetAsync(Key(articleId));
        if (!cached.HasValue)
            return null;

        await MarkAsReadAsync(articleId);
        return JsonSerializer.Deserialize<List<Comment>>(cached.ToString());
    }

    public async Task AddAsync(int articleId, List<Comment> comments)
    {
        await _cache.StringSetAsync(Key(articleId), JsonSerializer.Serialize(comments));
        await MarkAsReadAsync(articleId);
        await EvictLeastRecentlyUsedAsync();
    }

    public async Task RemoveAsync(int articleId)
    {
        await _cache.KeyDeleteAsync(Key(articleId));
        await _cache.SortedSetRemoveAsync(LastRead, articleId);
    }

    private Task MarkAsReadAsync(int articleId) =>
        _cache.SortedSetAddAsync(LastRead, articleId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    private async Task EvictLeastRecentlyUsedAsync()
    {
        var count = await _cache.SortedSetLengthAsync(LastRead);
        if (count > _maxArticles)
        {
            var leastRecentlyUsed = await _cache.SortedSetPopAsync(LastRead, count - _maxArticles, Order.Ascending);
            foreach (var entry in leastRecentlyUsed)
                await _cache.KeyDeleteAsync(Key((int)entry.Element));
            CacheMetrics.Evictions.WithLabels(CacheMetrics.Comment).Inc(leastRecentlyUsed.Length);
        }
        CacheMetrics.Entries.WithLabels(CacheMetrics.Comment).Set(Math.Min(count, _maxArticles));
    }
}
