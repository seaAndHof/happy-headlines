using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace ArticleService;

/// <summary>
/// The coordination component of the z-axis split. The ArticleDatabase is split by continent: seven continent
/// databases plus one global database for news that matters to the whole world. A request names its continent, and
/// the coordinator hands back the database that holds that continent's articles.
/// </summary>
public class ArticleDatabaseCoordinator
{
    public const string Global = "global";

    public static readonly IReadOnlyList<string> Continents =
        ["africa", "antarctica", "asia", "europe", "north-america", "oceania", "south-america", Global];

    // One connection pool per database, created once and shared by all requests.
    private readonly Dictionary<string, NpgsqlDataSource> _databases;

    public ArticleDatabaseCoordinator(IConfiguration config)
    {
        _databases = Continents.ToDictionary(continent => continent, continent => NpgsqlDataSource.Create(
            config[$"ArticleDatabases:{continent}"]
            ?? throw new InvalidOperationException($"No connection string for the {continent} ArticleDatabase")));
    }

    public bool TryGetDatabase(string continent, [NotNullWhen(true)] out NpgsqlDataSource? database) =>
        _databases.TryGetValue(continent.ToLowerInvariant(), out database);
}
