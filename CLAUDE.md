# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**WordApi** is a C# (.NET 9.0) ASP.NET Core minimal API that wraps a DAWG-compressed English dictionary (`dawg.bin`, 370,105 words) and exposes pattern-based word queries over HTTP.

The old console app (`WordScan`) has been moved to `Old code_WordScan/` for reference.

## Commands

```bash
dotnet build                                          # Build
dotnet run --project WordApi/WordApi.csproj          # Run (Kestrel on localhost:5236)
dotnet build --configuration Release                 # Release build
```

There are no test projects yet.

## File Structure

```
dawg_api/
├── dawg.bin                        # Binary DAWG dictionary (370,105 words, ~1.4 MB)
├── dawg_format.txt                 # Binary format specification for dawg.bin
├── README.md                       # User-facing usage guide
├── DawgDictionary-API.md           # DawgDictionary class API reference
├── Project.txt                     # Project brief / goals
├── Old code_WordScan/              # Previous console app (reference only)
│   ├── Program.cs
│   ├── WordScan.csproj
│   └── Dawg/
│       ├── DawgEdge.cs
│       └── DawgDictionary.cs
└── WordApi/                        # ASP.NET Core minimal API
    ├── WordApi.csproj              # Packages: Microsoft.AspNetCore.OpenApi 9.x, Scalar.AspNetCore
    ├── Program.cs                  # DI, OpenAPI/Scalar setup, endpoint registration
    └── Dawg/
        ├── DawgEdge.cs             # Readonly struct decoding a single uint32 DAWG edge
        └── DawgDictionary.cs       # Loads dawg.bin; exposes Count, Contains, Match, Anagram
```

## API Endpoints

### `GET /random`

Returns a single uniformly random word. Optional `length` parameter restricts to words of that exact letter count.

```
GET /random             → "serendipity"
GET /random?length=5    → "riven"
GET /random?length=12   → "enduringness"
```

### `GET /define/{word}`

Proxies the [Free Dictionary API](https://dictionaryapi.dev) — no key required. Returns phonetics, parts of speech, definitions, and examples.

```
GET /define/serendipity    → full JSON entry with phonetics and definitions
```

Note: `HttpClient` registered as named client `"dictionary"` via `AddHttpClient`.

### `GET /contains/{substring}`

Returns all words containing the given substring anywhere (min 2 chars). Enumerates all words via `Match("*")` and filters.

```
GET /contains/zzle    → ["dazzle","fizzle","puzzle","sizzle",...] (151 words)
```

### `GET /endswith/{suffix}`

Returns all words ending with the given suffix.

```
GET /endswith/ing    → ["aahing","abandoning",...] (18119 words)
```

### `GET /startswith/{prefix}`

Returns all words beginning with the given prefix.

```
GET /startswith/pre     → ["pre","preabsorb","preach",...] (4903 words)
```

### `GET /length/{n}`

Returns all words of exactly `n` letters (1–30).

```
GET /length/3    → ["aah","aal","aba",...] (2130 words)
GET /length/5    → all 5-letter words
```

### `GET /count`

Returns the total number of words in the dictionary.

```
GET /count    → 370105
```

### `GET /contains?word=<word>`

Exact lookup — returns `true` or `false`.

```
GET /contains?word=boxer        → true
GET /contains?word=xyzzy        → false
```

### `GET /words?pattern=<pattern>`

Pattern match — returns a JSON array of all words matching the pattern.

```
GET /words?pattern=???er        → ["boxer","cider","diner","diver",...]
GET /words?pattern=un*ing       → all words starting with "un" and ending in "ing"
```

Pattern syntax:
- `?` — any single letter (positional)
- `*` — any run of letters (variable length, one `*` per pattern)

### `GET /anagram?letters=<letters>`

Anagram search — returns all dictionary words that can be formed from exactly the given letters (word length = letters length).

```
GET /anagram?letters=stare      → ["aster","rates","stare","tears",...]
GET /anagram?letters=love?      → ["clove","glove","lover","novel","olive",...]
```

Use `?` in the letters pool as a wildcard tile (matches any letter).

## Architecture

`dawg.bin` is loaded **once at startup** and registered as a **singleton** via ASP.NET Core DI. Each `uint32` encodes one DAWG edge:

| Bits | Field | Notes |
|------|-------|-------|
| 0–4 | Character | 1='a' … 26='z' |
| 5 | End of Word | Set if traversing this edge completes a valid word |
| 6 | End of Node | Set if this is the last sibling in the current list |
| 7–31 | Next Pointer | Index of the child's sibling list; 0 = leaf |

**Key quirk:** index `0` means both the root node (at startup) and "no children" on a `NextIndex` field. These are distinguished by context — recursive calls are guarded by `edge.NextIndex != 0`, so `EnumerateSubtree` is only ever called with `0` for the initial root entry, never from a recursive leaf.

**Traversal:** Start at index 0. Scan the sibling list for a matching letter, follow `NextIndex` to descend, check `IsEndOfWord` on the last edge. The End of Word flag is on the edge, not the target node.

### DawgDictionary public API

- `Load(path)` — reads file, returns instance
- `Count` — total word count, computed via DFS traversal at load time
- `Random(rng)` — uniformly random word, selects Nth word in DFS order
- `Contains(word)` — exact lookup, O(word length)
- `Match(pattern)` — positional (`?` = any single letter) and star (`*` = any run of letters, one per pattern)
- `Anagram(letters)` — bag-based DFS; `?` in the pool is a wildcard tile; word length = pool length

Full API reference: `DawgDictionary-API.md`. Full format spec: `dawg_format.txt`.

## API Docs

- Scalar UI: `http://localhost:5236/scalar/v1` — interactive docs with live test client
- OpenAPI JSON: `http://localhost:5236/openapi/v1.json`

## Design Decisions

- ASP.NET Core minimal API (not MVC) — clean and lightweight
- `DawgDictionary` registered as a singleton service via DI
- `dawg.bin` path resolved via `AppContext.BaseDirectory` (output dir) — not `ContentRootPath`
- OpenAPI via `Microsoft.AspNetCore.OpenApi` 9.x (pin to `9.*` — NuGet defaults to 10.x which requires .NET 10)
- Scalar UI via `Scalar.AspNetCore` for interactive docs
- Local hosting with `dotnet run` is sufficient for now (future: MonoGame word game backend)
