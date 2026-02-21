# dawg-api

An ASP.NET Core minimal API for querying a 370,105-word English dictionary stored as a binary DAWG (Directed Acyclic Word Graph).

## Build & Run

```bash
dotnet run --project WordApi/WordApi.csproj
```

Starts Kestrel on `http://localhost:5236`.

## API Docs

Interactive docs (Scalar UI): **http://localhost:5236/scalar/v1**

The Scalar page includes a full endpoint table, syntax guide, and a live test client for each endpoint.

Raw OpenAPI JSON: http://localhost:5236/openapi/v1.json

---

## Endpoints

### Lookup

#### `GET /count`
Returns the total number of words in the dictionary.
```
GET /count    → 370105
```

#### `GET /contains?word=<word>`
Exact dictionary lookup — returns `true` or `false`.
```
GET /contains?word=boxer    → true
GET /contains?word=xyzzy    → false
```

#### `GET /define/{word}`
Returns the definition via the [Free Dictionary API](https://dictionaryapi.dev). Includes phonetics, parts of speech, definitions, and example sentences.
```
GET /define/serendipity    → phonetics, noun definitions, examples
GET /define/xyzzy          → 404 if no definition found
```

---

### Search

#### `GET /words?pattern=<pattern>`
Returns all words matching the pattern. `?` = any single letter, `*` = any run of letters (one per pattern).
```
GET /words?pattern=???er     → 5-letter words ending in "er"
GET /words?pattern=un*ing    → words starting with "un", ending with "ing"
```

#### `GET /anagram?letters=<letters>`
Returns all words that can be formed from exactly the given letters. Word length = number of letters. Use `?` as a wildcard tile.
```
GET /anagram?letters=stare    → ["aster","rates","stare","tears",...]
GET /anagram?letters=love?    → 5-letter words using l,o,v,e + any one letter
```

#### `GET /length/{n}`
Returns all words of exactly `n` letters.
```
GET /length/3    → ["aah","aal","aba",...] (2130 words)
```

#### `GET /startswith/{prefix}`
Returns all words beginning with the given prefix.
```
GET /startswith/pre    → ["pre","preabsorb","preach",...] (4903 words)
```

#### `GET /endswith/{suffix}`
Returns all words ending with the given suffix.
```
GET /endswith/ing    → ["aahing","abandoning",...] (18119 words)
```

#### `GET /contains/{substring}`
Returns all words containing the substring anywhere. Minimum 2 characters.
```
GET /contains/zzle    → ["dazzle","fizzle","puzzle","sizzle",...] (151 words)
```

#### `GET /rhymes/{word}`
Returns words that rhyme — matched by spelling from the last vowel onward (spelling-based, not phonetic).
```
GET /rhymes/light    → suffix "ight" → ["blight","bright","fight","night",...] (339 words)
GET /rhymes/cat      → suffix "at"  → ["bat","chat","fat","hat","mat","rat",...]
```

---

### Random & Daily

#### `GET /random`
Returns a single uniformly random word. Optionally restrict to a specific length.
```
GET /random             → "serendipity"
GET /random?length=5    → "riven"
```

#### `GET /word-of-the-day`
Returns a deterministic word for the current UTC date — same for all callers, changes at midnight UTC.
```json
GET /word-of-the-day    → {"date":"2026-02-21","word":"asklent"}
```

---

### Games

#### `GET /quiz`
Returns a word scramble puzzle — shuffled letters, hint, and answer for client-side reveal. Optional `length` parameter.
```json
GET /quiz           → {"scrambled":"rtaes","length":5,"hint":"Starts with 's'","answer":"stare"}
GET /quiz?length=5  → random 5-letter scramble
```

#### `GET /wordle/daily`
Today's 5-letter Wordle word — deterministic per UTC date, same for all callers.
```json
GET /wordle/daily    → {"date":"2026-02-21","word":"ergon"}
```

#### `GET /wordle/check`
Scores a guess against an answer using Wordle rules. Both words must be the same length and valid dictionary words.
```json
GET /wordle/check?answer=stare&guess=crane
→ {
    "guess": "crane",
    "result": [
      {"letter":"c","result":"absent"},
      {"letter":"r","result":"present"},
      {"letter":"a","result":"correct"},
      {"letter":"n","result":"absent"},
      {"letter":"e","result":"correct"}
    ],
    "solved": false
  }
```
Per-letter results: `correct` (right position), `present` (in word, wrong position), `absent` (not in word). Handles duplicate letters correctly.

---

## Dictionary

`dawg.bin` — 370,105 English words compressed into a ~1.4 MB binary DAWG. Each node is a packed `uint32` encoding the letter, end-of-word flag, end-of-node flag, and child index.
