using Npgsql;

namespace ArticleService;

public class ArticleDatabaseCoordinator
{
    public const string Global = "global";

    public static readonly IReadOnlyList<string> Continents =
        ["africa", "antarctica", "asia", "europe", "north-america", "oceania", "south-america", Global];

    private readonly Dictionary<string, NpgsqlDataSource> _databases = new();

    public ArticleDatabaseCoordinator(IConfiguration config)
    {
        foreach (var continent in Continents)
        {
            var connectionString = config[$"ArticleDatabases:{continent}"]
                ?? throw new InvalidOperationException($"No connection string for the {continent} ArticleDatabase");
            _databases[continent] = NpgsqlDataSource.Create(connectionString);
        }
    }

    public NpgsqlDataSource? GetDatabase(string continent) => _databases.GetValueOrDefault(continent);
}
