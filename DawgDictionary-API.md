# DawgDictionary — API Reference

`DawgDictionary` loads `dawg.bin` into memory and exposes three search methods.
All input is case-insensitive (normalised to lowercase internally).
Results are always returned in alphabetical order (a consequence of DAWG traversal order).

---

## Loading

```csharp
DawgDictionary dawg = DawgDictionary.Load("path/to/dawg.bin");
```

Reads the entire binary file into a `uint[]` array once. After that, all searches
are in-memory with no further I/O.

```csharp
int count = dawg.EdgeCount;   // 370 413 — total edges in the DAWG
```

---

## Contains — exact lookup

```csharp
bool found = dawg.Contains(word);
```

Returns `true` if the word exists in the dictionary. Complexity is O(word length).

```csharp
dawg.Contains("tower")   // true
dawg.Contains("towerx")  // false
dawg.Contains("TOWER")   // true  (case-insensitive)
```

---

## Match — pattern search

```csharp
List<string> results = dawg.Match(pattern);
```

Supports two wildcard characters:

| Character | Meaning |
|-----------|---------|
| `?` | any single letter (positional) |
| `*` | zero or more letters (at most one per pattern) |

Without wildcards `Match` behaves like `Contains` but returns a list.

### Positional patterns (`?`)

The pattern length is the exact length of words returned.

```csharp
dawg.Match("to???")   // 5-letter words starting with "to"
dawg.Match("???er")   // 5-letter words ending with "er"
dawg.Match("t??e?")   // 5-letter words: starts with "t", 4th letter "e"
dawg.Match("?????")   // all 5-letter words
dawg.Match("tower")   // exact lookup — returns ["tower"] or []
```

### Star patterns (`*`)

```csharp
dawg.Match("to*")     // all words starting with "to" (any length)
dawg.Match("*er")     // all words ending with "er" (any length)
dawg.Match("to*er")   // words starting with "to" and ending with "er"
dawg.Match("*")       // entire dictionary
```

`*` matches zero letters too, so `"to*"` includes `"to"` itself if it is a word.
Only one `*` per pattern is supported.

---

## Anagram — letter-pool search

```csharp
List<string> results = dawg.Anagram(letters);
```

Finds all valid words that can be formed using exactly the letters in the pool.
Word length equals `letters.Length`. Use `?` as a wildcard tile (matches any letter).

```csharp
dawg.Anagram("love")    // 4-letter anagrams of "love"  → vole, love, ...
dawg.Anagram("love?")   // 5-letter words using l,o,v,e + any one letter
                        //   → lover, novel, glove, olive, solve, ...
dawg.Anagram("love??")  // 6-letter words using l,o,v,e + any two letters
dawg.Anagram("?????")   // all 5-letter words (all wildcards = same as Match("?????"))
```

Each letter in the pool is consumed exactly once. Duplicate letters in the pool
are treated as separate tiles, so `"oo"` provides two o's.

```csharp
dawg.Anagram("aabcd")   // words using a, a, b, c, d  (two a's available)
```

---

## How the three methods compare

| Method | Length constraint | Letter positions fixed | Letter pool |
|--------|-------------------|----------------------|-------------|
| `Contains` | exact | yes | n/a |
| `Match("to???")` | exact (= pattern length) | yes (`?` = any) | n/a |
| `Match("to*")` | any | prefix/suffix fixed | n/a |
| `Anagram("love?")` | exact (= pool size) | no | yes |

---

## Internal design notes

- The DAWG is a flat `uint[]`. Each element is one edge encoding a letter,
  end-of-word flag, end-of-node flag, and a child pointer (see `dawg_format.txt`).
- `DawgEdge` is a thin readonly struct that decodes a raw `uint` on the fly — no
  separate object allocation per edge.
- `Match` with `?` uses recursive DFS, pruning entire subtrees when a fixed letter
  doesn't match. `Match` with `*` navigates to the prefix node first, then
  enumerates the subtree filtering by suffix.
- `Anagram` carries a `int[26]` letter-count bag through the DFS. At each edge it
  checks whether the edge letter is still available in the bag (or a wildcard slot
  remains), consumes it, recurses, then restores — standard backtracking.
- Index `0` is overloaded in the DAWG format: it is both the root node's start
  index and the sentinel meaning "no children" on a `NextIndex` field. Traversal
  methods distinguish the two cases by context (initial call vs. recursive call
  guarded by `edge.NextIndex != 0`).
