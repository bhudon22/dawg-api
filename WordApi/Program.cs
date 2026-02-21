using Scalar.AspNetCore;
using WordApi.Dawg;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info.Title       = "DAWG Word API";
        doc.Info.Description = "Query a 370,105-word English dictionary compressed as a binary DAWG.";
        doc.Info.Version     = "v1";
        return Task.CompletedTask;
    });
});

// Register the DAWG dictionary as a singleton — loaded once at startup.
builder.Services.AddSingleton<DawgDictionary>(_ =>
{
    string path = Path.Combine(AppContext.BaseDirectory, "dawg.bin");
    return DawgDictionary.Load(path);
});

var app = builder.Build();

// Eagerly load the dictionary during startup so the first request isn't slow.
app.Services.GetRequiredService<DawgDictionary>();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "DAWG Word API";
});

// GET /contains?word=boxer
app.MapGet("/contains", (string word, DawgDictionary dawg) =>
{
    if (string.IsNullOrEmpty(word))
        return Results.BadRequest("word query parameter is required.");

    return Results.Ok(dawg.Contains(word));
})
.WithName("Contains")
.WithSummary("Exact word lookup")
.WithDescription("Returns true if the word exists in the dictionary, false otherwise.");

// GET /words?pattern=???er
app.MapGet("/words", (string pattern, DawgDictionary dawg) =>
{
    if (string.IsNullOrEmpty(pattern))
        return Results.BadRequest("pattern query parameter is required.");

    List<string> matches = dawg.Match(pattern);
    return Results.Ok(matches);
})
.WithName("Words")
.WithSummary("Pattern match")
.WithDescription(
    "Returns all words matching the pattern. " +
    "Use `?` for any single letter (positional), " +
    "or `*` for any run of letters (at most one `*` per pattern).");

// GET /anagram?letters=love?
app.MapGet("/anagram", (string letters, DawgDictionary dawg) =>
{
    if (string.IsNullOrEmpty(letters))
        return Results.BadRequest("letters query parameter is required.");

    List<string> matches = dawg.Anagram(letters);
    return Results.Ok(matches);
})
.WithName("Anagram")
.WithSummary("Anagram search")
.WithDescription(
    "Returns all words that can be formed from exactly the given letters. " +
    "Word length equals the number of letters supplied. " +
    "Use `?` as a wildcard tile (matches any letter).");

app.Run();
