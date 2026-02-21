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

### Lookup

| Endpoint | Description |
|---|---|
| `GET /count` | Total word count → `370105` |
| `GET /contains?word=boxer` | Exact lookup → `true`/`false` |
| `GET /define/{word}` | Definition via dictionaryapi.dev (no key) |

### Search

| Endpoint | Description |
|---|---|
| `GET /words?pattern=???er` | Pattern match (`?`=single letter, `*`=run of letters) |
| `GET /anagram?letters=stare` | Anagram search (`?`=wildcard tile, word length = letters length) |
| `GET /length/{n}` | All words of exactly n letters (1–30) |
| `GET /startswith/{prefix}` | All words beginning with prefix |
| `GET /endswith/{suffix}` | All words ending with suffix |
| `GET /contains/{substring}` | All words containing substring (min 2 chars, enumerates all via `Match("*")`) |
| `GET /rhymes/{word}` | Spelling-based rhyme: suffix from last vowel onward → `{ word, suffix, rhymes[] }` |

### Random / Daily

| Endpoint | Description |
|---|---|
| `GET /random` | Uniformly random word (selects Nth word in DFS order) |
| `GET /random?length=5` | Random word of exactly 5 letters |
| `GET /word-of-the-day` | Deterministic daily word → `{ date, word }` (seeded by UTC day number) |

### Games

| Endpoint | Description |
|---|---|
| `GET /quiz` | Word scramble: Fisher-Yates shuffle → `{ scrambled, length, hint, answer }` |
| `GET /quiz?length=5` | Scramble restricted to 5-letter words |
| `GET /wordle/daily` | Today's 5-letter Wordle word (deterministic, `dayNumber % words.Count`) |
| `GET /wordle/check?answer=stare&guess=crane` | Two-pass Wordle scorer → per-letter `correct`/`present`/`absent` + `solved` |

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
