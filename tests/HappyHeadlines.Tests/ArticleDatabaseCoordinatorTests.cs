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
        Assert.True(Coordinator.TryGetDatabase(continent, out var database));
        Assert.Contains($"Host=db-{continent}", database.ConnectionString);
    }

    [Fact]
    public void Continent_names_are_case_insensitive()
    {
        Assert.True(Coordinator.TryGetDatabase("Europe", out var database));
        Assert.Contains("Host=db-europe", database.ConnectionString);
    }

    [Fact]
    public void Rejects_an_unknown_continent()
    {
        Assert.False(Coordinator.TryGetDatabase("atlantis", out _));
    }
}
