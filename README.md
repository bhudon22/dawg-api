# WordScan

A console application for exploring an English dictionary of 370,105 words stored as a binary DAWG (Directed Acyclic Word Graph). Use it to look up words, search by pattern, or find anagrams.

## Build & Run

```
dotnet build
dotnet run --project WordScan/WordScan.csproj
```

## Pattern Syntax

Type a pattern at the `Pattern>` prompt and press Enter.

### Exact lookup

```
Pattern> cat
```

Returns whether "cat" exists in the dictionary.

### Positional patterns — `?` matches any single letter

```
Pattern> to???        five-letter words starting with "to"
Pattern> ???er        five-letter words ending with "er"
Pattern> t??e?        five-letter words: starts with "t", fourth letter "e"
Pattern> ?????        all five-letter words
```

The length of the pattern is the exact length of the words returned.

### Star patterns — `*` matches zero or more letters (one `*` per pattern)

```
Pattern> to*          all words starting with "to" (any length)
Pattern> *er          all words ending with "er" (any length)
Pattern> to*er        words starting with "to" and ending with "er"
```

### Anagram search — `{letters}` matches any arrangement of the letter pool

Wrap the letter pool in curly braces. The number of characters inside is the exact length of the words returned. Use `?` inside the braces as a wildcard tile (any letter).

```
Pattern> {love}       four-letter words using exactly the letters l, o, v, e
Pattern> {love?}      five-letter words using l, o, v, e, plus any one letter
Pattern> {love??}     six-letter words using l, o, v, e, plus any two letters
```

## Summary Table

| Pattern    | Meaning                                              |
|------------|------------------------------------------------------|
| `cat`      | exact lookup                                         |
| `to???`    | exactly 5 letters, starting with "to"               |
| `???er`    | exactly 5 letters, ending with "er"                 |
| `?o?e?`    | exactly 5 letters, 2nd letter "o", 4th letter "e"   |
| `to*`      | any length, starting with "to"                       |
| `*er`      | any length, ending with "er"                         |
| `to*er`    | any length, starting with "to" and ending with "er"  |
| `{love}`   | 4-letter anagrams of "love"                          |
| `{love?}`  | 5-letter anagrams: l, o, v, e + 1 wildcard tile     |

Type `q` or `quit` to exit.
