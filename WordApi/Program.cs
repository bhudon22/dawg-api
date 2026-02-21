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
            | `GET /rhymes/{word}` | `/rhymes/light` | Words that rhyme (spelling-based) |
            | `GET /wordle/daily` | `/wordle/daily` | Today's 5-letter Wordle word |
            | `GET /wordle/check` | `/wordle/check?answer=stare&guess=crane` | Score a Wordle guess |
            | `GET /quiz` | `/quiz` or `/quiz?length=5` | Word scramble puzzle |
            | `GET /word-of-the-day` | `/word-of-the-day` | Same word for everyone today (changes midnight UTC) |
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

// GET /rhymes/light
app.MapGet("/rhymes/{word}", (string word, DawgDictionary dawg) =>
{
    word = word.ToLowerInvariant();

    if (!dawg.Contains(word))
        return Results.NotFound($"'{word}' is not in the dictionary.");

    // Rhyme suffix = everything from the last vowel onward.
    int lastVowel = -1;
    for (int i = word.Length - 1; i >= 0; i--)
        if ("aeiou".Contains(word[i])) { lastVowel = i; break; }

    if (lastVowel == -1)
        return Results.BadRequest($"'{word}' has no vowel — cannot determine rhyme.");

    string suffix = word[lastVowel..];
    List<string> rhymes = dawg.Match("*" + suffix)
        .Where(w => w != word)
        .ToList();

    return Results.Ok(new { word, suffix, rhymes });
})
.WithName("Rhymes")
.WithSummary("Rhyming words")
.WithDescription(
    "Returns words that rhyme with the given word. " +
    "Rhyme is approximated by matching the spelling from the last vowel onward " +
    "(e.g. 'light' → suffix 'ight'). Spelling-based, not phonetic.");

// GET /wordle/daily
app.MapGet("/wordle/daily", (DawgDictionary dawg) =>
{
    int dayNumber        = (int)(DateTime.UtcNow.Date - DateTime.UnixEpoch).TotalDays;
    List<string> words   = dawg.Match("?????");
    string word          = words[dayNumber % words.Count];
    return Results.Ok(new { date = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"), word });
})
.WithName("WordleDaily")
.WithSummary("Wordle daily word")
.WithDescription("Returns today's 5-letter Wordle word. Deterministic per UTC date — same for all callers on the same day. Use with /wordle/check to validate guesses.");

// GET /wordle/check?answer=stare&guess=crane
app.MapGet("/wordle/check", (string answer, string guess, DawgDictionary dawg) =>
{
    answer = answer.ToLowerInvariant();
    guess  = guess.ToLowerInvariant();

    if (answer.Length != guess.Length)
        return Results.BadRequest("answer and guess must be the same length.");
    if (answer.Length < 2 || answer.Length > 15)
        return Results.BadRequest("word length must be between 2 and 15.");
    if (!dawg.Contains(answer))
        return Results.BadRequest($"'{answer}' is not in the dictionary.");
    if (!dawg.Contains(guess))
        return Results.BadRequest($"'{guess}' is not in the dictionary.");

    // Wordle scoring: two-pass to handle duplicate letters correctly.
    var    results    = new string[guess.Length];
    int[]  answerPool = new int[26];

    // Pass 1 — exact matches (correct), build remaining letter pool.
    for (int i = 0; i < guess.Length; i++)
    {
        if (guess[i] == answer[i]) results[i] = "correct";
        else                       answerPool[answer[i] - 'a']++;
    }

    // Pass 2 — present or absent.
    for (int i = 0; i < guess.Length; i++)
    {
        if (results[i] != null) continue;
        int li = guess[i] - 'a';
        if (answerPool[li] > 0) { results[i] = "present"; answerPool[li]--; }
        else                      results[i] = "absent";
    }

    return Results.Ok(new
    {
        guess,
        result = guess.Select((c, i) => new { letter = c.ToString(), result = results[i] }),
        solved = results.All(r => r == "correct")
    });
})
.WithName("WordleCheck")
.WithSummary("Wordle guess checker")
.WithDescription(
    "Scores a guess against the answer using Wordle rules. " +
    "Each letter gets: `correct` (right position), `present` (in word, wrong position), or `absent` (not in word). " +
    "Handles duplicate letters correctly with a two-pass algorithm.");

// GET /quiz
// GET /quiz?length=5
app.MapGet("/quiz", (DawgDictionary dawg, int? length) =>
{
    string word = length.HasValue
        ? dawg.Match(new string('?', length.Value)) is { Count: > 0 } pool
            ? pool[rng.Next(pool.Count)]
            : null!
        : dawg.Random(rng);

    if (word is null)
        return Results.NotFound($"No words of length {length} in the dictionary.");

    // Fisher-Yates shuffle
    char[] letters = word.ToCharArray();
    for (int i = letters.Length - 1; i > 0; i--)
    {
        int j = rng.Next(i + 1);
        (letters[i], letters[j]) = (letters[j], letters[i]);
    }

    return Results.Ok(new
    {
        scrambled = new string(letters),
        length    = word.Length,
        hint      = $"Starts with '{word[0]}'",
        answer    = word
    });
})
.WithName("Quiz")
.WithSummary("Word scramble quiz")
.WithDescription(
    "Returns a scrambled word puzzle. " +
    "Optionally supply `length` to control word length. " +
    "The response includes the scrambled letters, a hint, and the answer for client-side reveal.");

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

// GET /word-of-the-day
app.MapGet("/word-of-the-day", (DawgDictionary dawg) =>
{
    int dayNumber = (int)(DateTime.UtcNow.Date - DateTime.UnixEpoch).TotalDays;
    string word   = dawg.Random(new System.Random(dayNumber));
    return Results.Ok(new { date = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"), word });
})
.WithName("WordOfTheDay")
.WithSummary("Word of the day")
.WithDescription("Returns a deterministic word for the current UTC date. The word is the same for all callers on the same day and changes at midnight UTC.");

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
