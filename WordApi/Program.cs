using Scalar.AspNetCore;
using WordApi.Dawg;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info.Title       = "DAWG Word API";
        doc.Info.Description = """
            Query a 370,105-word English dictionary compressed as a binary DAWG.

            ## Endpoints

            | Endpoint | Example | Description |
            |---|---|---|
            | `GET /random` | `/random` or `/random?length=5` | Random word (optional exact length) |
            | `GET /length/{n}` | `/length/5` | All words of exactly n letters |
            | `GET /startswith/{prefix}` | `/startswith/pre` | All words beginning with prefix |
            | `GET /endswith/{suffix}` | `/endswith/ing` | All words ending with suffix |
            | `GET /contains/{substring}` | `/contains/zzle` | All words containing substring |
            | `GET /define/{word}` | `/define/serendipity` | Word definition (via dictionaryapi.dev) |
            | `GET /count` | `/count` | Total word count |
            | `GET /contains` | `/contains?word=boxer` | Exact lookup — returns `true`/`false` |
            | `GET /words` | `/words?pattern=???er` | Pattern match — returns matching words |
            | `GET /anagram` | `/anagram?letters=stare` | Anagram search — returns all arrangements |

            ## Pattern Syntax

            - `?` — any single letter: `???er` → 5-letter words ending in "er"
            - `*` — any run of letters (one per pattern): `un*ing` → words starting with "un", ending with "ing"

            ## Anagram Syntax

            - `stare` → all words using exactly s, t, a, r, e
            - `love?` → 5-letter words using l, o, v, e + one wildcard tile
            """;
        doc.Info.Version     = "v1";
        return Task.CompletedTask;
    });
});

// HttpClient for the Free Dictionary API.
builder.Services.AddHttpClient("dictionary", c =>
{
    c.BaseAddress = new Uri("https://api.dictionaryapi.dev/");
    c.DefaultRequestHeaders.Add("Accept", "application/json");
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

// GET /random
// GET /random?length=5
var rng = System.Random.Shared;
app.MapGet("/random", (DawgDictionary dawg, int? length) =>
{
    if (length.HasValue)
    {
        if (length.Value < 1 || length.Value > 30)
            return Results.BadRequest("length must be between 1 and 30.");

        List<string> words = dawg.Match(new string('?', length.Value));
        if (words.Count == 0)
            return Results.NotFound($"No words of length {length.Value} in the dictionary.");

        return Results.Ok(words[rng.Next(words.Count)]);
    }
    return Results.Ok(dawg.Random(rng));
})
.WithName("Random")
.WithSummary("Random word")
.WithDescription(
    "Returns a single uniformly random word from the dictionary. " +
    "Optionally supply `length` to restrict to words of that exact letter count.");

// GET /define/serendipity
app.MapGet("/define/{word}", async (string word, IHttpClientFactory httpFactory) =>
{
    var client   = httpFactory.CreateClient("dictionary");
    var response = await client.GetAsync($"api/v2/entries/en/{Uri.EscapeDataString(word)}");

    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        return Results.NotFound($"No definition found for '{word}'.");

    if (!response.IsSuccessStatusCode)
        return Results.StatusCode((int)response.StatusCode);

    var json = await response.Content.ReadAsStringAsync();
    return Results.Content(json, "application/json");
})
.WithName("Define")
.WithSummary("Word definition")
.WithDescription("Returns definitions from the Free Dictionary API (dictionaryapi.dev). Proxies the full response including phonetics, parts of speech, and example sentences.");

// GET /contains/zzl
app.MapGet("/contains/{substring}", (string substring, DawgDictionary dawg) =>
{
    if (substring.Length < 2 || substring.Length > 30)
        return Results.BadRequest("substring must be between 2 and 30 characters.");

    List<string> matches = dawg.Match("*")
        .Where(w => w.Contains(substring, StringComparison.Ordinal))
        .ToList();
    return Results.Ok(matches);
})
.WithName("ContainsSubstring")
.WithSummary("Words containing substring")
.WithDescription("Returns all words that contain the given substring anywhere. Minimum 2 characters.");

// GET /endswith/ing
app.MapGet("/endswith/{suffix}", (string suffix, DawgDictionary dawg) =>
{
    if (suffix.Length > 30)
        return Results.BadRequest("suffix must be 30 characters or fewer.");

    List<string> words = dawg.Match("*" + suffix);
    return Results.Ok(words);
})
.WithName("EndsWith")
.WithSummary("Words ending with suffix")
.WithDescription("Returns all words that end with the given suffix.");

// GET /startswith/pre
app.MapGet("/startswith/{prefix}", (string prefix, DawgDictionary dawg) =>
{
    if (prefix.Length > 30)
        return Results.BadRequest("prefix must be 30 characters or fewer.");

    List<string> words = dawg.Match(prefix + "*");
    return Results.Ok(words);
})
.WithName("StartsWith")
.WithSummary("Words starting with prefix")
.WithDescription("Returns all words that begin with the given prefix.");

// GET /length/5
app.MapGet("/length/{n:int}", (int n, DawgDictionary dawg) =>
{
    if (n < 1 || n > 30)
        return Results.BadRequest("Length must be between 1 and 30.");

    List<string> words = dawg.Match(new string('?', n));
    return Results.Ok(words);
})
.WithName("ByLength")
.WithSummary("Words by length")
.WithDescription("Returns all words of exactly the given letter count.");

// GET /count
app.MapGet("/count", (DawgDictionary dawg) => Results.Ok(dawg.Count))
.WithName("Count")
.WithSummary("Word count")
.WithDescription("Returns the total number of words in the dictionary.");

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
