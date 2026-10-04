namespace HappyHeadlines.Shared;

public static class CacheKeys
{
    public static string Article(int id) => $"article:{id}";
}
