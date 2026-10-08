using Dapper;
using HappyHeadlines.Shared;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(NpgsqlDataSource.Create(builder.Configuration["ProfanityDatabase"]!));

var app = builder.Build();

// Masks every word from the ProfanityDatabase in the text it's given.
app.MapPost("/filter", async (FilterRequest request, NpgsqlDataSource database) =>
{
    await using var connection = await database.OpenConnectionAsync();
    var words = await connection.QueryAsync<string>("SELECT word FROM profanities");
    return new FilterResponse(ProfanityFilter.Mask(request.Text, words));
});

app.Run();
