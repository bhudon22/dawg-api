using WordApi.Dawg;

var builder = WebApplication.CreateBuilder(args);

// Register the DAWG dictionary as a singleton — loaded once at startup.
builder.Services.AddSingleton<DawgDictionary>(_ =>
{
    string path = Path.Combine(AppContext.BaseDirectory, "dawg.bin");
    return DawgDictionary.Load(path);
});

var app = builder.Build();

// Eagerly load the dictionary during startup so the first request isn't slow.
app.Services.GetRequiredService<DawgDictionary>();

// GET /words?pattern=???er
// Returns a JSON array of all words matching the pattern.
// Pattern syntax: ? = any single letter, * = any run of letters (one per pattern).
app.MapGet("/words", (string pattern, DawgDictionary dawg) =>
{
    if (string.IsNullOrEmpty(pattern))
        return Results.BadRequest("pattern query parameter is required.");

    List<string> matches = dawg.Match(pattern);
    return Results.Ok(matches);
});

// GET /contains?word=boxer
// Returns 200 true/false indicating whether the word exists in the dictionary.
app.MapGet("/contains", (string word, DawgDictionary dawg) =>
{
    if (string.IsNullOrEmpty(word))
        return Results.BadRequest("word query parameter is required.");

    return Results.Ok(dawg.Contains(word));
});

// GET /anagram?letters=love?
// Returns all words that can be formed from the given letters (exact length match).
// Use ? for a wildcard tile.
app.MapGet("/anagram", (string letters, DawgDictionary dawg) =>
{
    if (string.IsNullOrEmpty(letters))
        return Results.BadRequest("letters query parameter is required.");

    List<string> matches = dawg.Anagram(letters);
    return Results.Ok(matches);
});

app.Run();
