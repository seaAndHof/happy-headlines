using ArticleService;
using Microsoft.Extensions.Configuration;

namespace HappyHeadlines.Tests;

public class ArticleDatabaseCoordinatorTests
{
    private static readonly ArticleDatabaseCoordinator Coordinator = new(new ConfigurationBuilder()
        .AddInMemoryCollection(ArticleDatabaseCoordinator.Continents
            .Select(c => KeyValuePair.Create($"ArticleDatabases:{c}", (string?)$"Host=db-{c};Database=articles")))
        .Build());

    [Theory]
    [InlineData("africa")]
    [InlineData("antarctica")]
    [InlineData("asia")]
    [InlineData("europe")]
    [InlineData("north-america")]
    [InlineData("oceania")]
    [InlineData("south-america")]
    [InlineData("global")]
    public void Routes_each_continent_to_its_own_database(string continent)
    {
        var database = Coordinator.GetDatabase(continent);

        Assert.NotNull(database);
        Assert.Contains($"Host=db-{continent}", database.ConnectionString);
    }

    [Theory]
    [InlineData("atlantis")]
    [InlineData("Europe")] // names match exactly, so "GLOBAL" can't slip past the ArticleCache
    public void Rejects_an_unknown_continent(string continent)
    {
        Assert.Null(Coordinator.GetDatabase(continent));
    }
}
