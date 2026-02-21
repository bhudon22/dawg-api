# dawg-api

An ASP.NET Core minimal API for querying a 370,105-word English dictionary stored as a binary DAWG (Directed Acyclic Word Graph).

## Build & Run

```bash
dotnet run --project WordApi/WordApi.csproj
```

Starts Kestrel on `http://localhost:5236`.

## API Docs

Interactive docs (Scalar UI): **http://localhost:5236/scalar/v1**

The Scalar page includes a full endpoint table, pattern/anagram syntax guide, and a live test client for each endpoint.

Raw OpenAPI JSON: http://localhost:5236/openapi/v1.json

## Endpoints

### `GET /random`

Returns a single uniformly random word from the dictionary. Optionally restrict to a specific length.

```
GET /random             → "serendipity"
GET /random?length=5    → "riven"
GET /random?length=12   → "enduringness"
```

---

### `GET /contains/{substring}`

Returns all words containing the given substring anywhere. Minimum 2 characters.

```
GET /contains/zzle    → ["dazzle","fizzle","puzzle","sizzle",...] (151 words)
GET /contains/tion    → all words containing "tion"
```

---

### `GET /endswith/{suffix}`

Returns all words ending with the given suffix.

```
GET /endswith/ing    → ["aahing","abandoning",...] (18119 words)
GET /endswith/tion   → all words ending with "tion"
```

---

### `GET /startswith/{prefix}`

Returns all words beginning with the given prefix.

```
GET /startswith/pre     → ["pre","preabsorb","preach",...] (4903 words)
GET /startswith/un      → all words starting with "un"
```

---

### `GET /length/{n}`

Returns all words of exactly `n` letters.

```
GET /length/3    → ["aah","aal","aba",...] (2130 words)
GET /length/5    → all 5-letter words
```

---

### `GET /count`

Returns the total number of words in the dictionary.

```
GET /count    → 370105
```

---

### `GET /contains?word=<word>`

Returns `true` or `false` — exact dictionary lookup.

```
GET /contains?word=boxer    → true
GET /contains?word=xyzzy    → false
```

---

### `GET /words?pattern=<pattern>`

Returns a JSON array of all words matching the pattern.

**`?` — any single letter (positional)**

```
GET /words?pattern=???er        → 5-letter words ending in "er"
GET /words?pattern=to???        → 5-letter words starting with "to"
GET /words?pattern=?????        → all 5-letter words
```

**`*` — any run of letters (one `*` per pattern)**

```
GET /words?pattern=to*          → all words starting with "to"
GET /words?pattern=*er          → all words ending with "er"
GET /words?pattern=un*ing       → words starting with "un" and ending with "ing"
```

---

### `GET /anagram?letters=<letters>`

Returns all words that can be formed from exactly the given letters. Word length equals the number of letters supplied. Use `?` as a wildcard tile.

```
GET /anagram?letters=stare      → ["aster","rates","stare","tears",...]
GET /anagram?letters=love       → 4-letter anagrams of l, o, v, e
GET /anagram?letters=love?      → 5-letter words using l, o, v, e + any one letter
```

## Pattern Reference

| Example | Meaning |
|---|---|
| `???er` | exactly 5 letters, ending with "er" |
| `to???` | exactly 5 letters, starting with "to" |
| `to*` | any length, starting with "to" |
| `*er` | any length, ending with "er" |
| `to*er` | any length, starting with "to" and ending with "er" |
| `stare` | anagram — all arrangements of s, t, a, r, e |
| `love?` | anagram — l, o, v, e + one wildcard tile |

## Dictionary

`dawg.bin` — 370,105 English words compressed into a ~1.4 MB binary DAWG. Each node is a packed `uint32` encoding the letter, end-of-word flag, end-of-node flag, and child index.
