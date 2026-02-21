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
└── WordApi/                        # NEW — ASP.NET Core minimal API
    ├── WordApi.csproj
    ├── Program.cs                  # Minimal API setup, DI, endpoint registration
    └── Dawg/
        ├── DawgEdge.cs             # Readonly struct decoding a single uint32 DAWG edge
        └── DawgDictionary.cs       # Loads dawg.bin; exposes Contains, Match, Anagram
```

## API Endpoints

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
- `Contains(word)` — exact lookup, O(word length)
- `Match(pattern)` — positional (`?` = any single letter) and star (`*` = any run of letters, one per pattern)
- `Anagram(letters)` — bag-based DFS; `?` in the pool is a wildcard tile; word length = pool length

Full API reference: `DawgDictionary-API.md`. Full format spec: `dawg_format.txt`.

## Design Decisions

- ASP.NET Core minimal API (not MVC) — clean and lightweight
- `DawgDictionary` registered as a singleton service via DI
- `dawg.bin` path resolved via `AppContext.BaseDirectory` (output dir) — not `ContentRootPath`
- Local hosting with `dotnet run` is sufficient for now (future: MonoGame word game backend)
