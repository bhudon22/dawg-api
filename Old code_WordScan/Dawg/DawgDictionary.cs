namespace WordScan.Dawg;

public class DawgDictionary
{
    private readonly uint[] _data;

    private DawgDictionary(uint[] data) => _data = data;

    public static DawgDictionary Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        uint[] data  = new uint[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
        return new DawgDictionary(data);
    }

    public int EdgeCount => _data.Length;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Exact word lookup. O(word length).</summary>
    public bool Contains(string word)
    {
        word = word.ToLowerInvariant();
        uint node = 0;
        for (int i = 0; i < word.Length; i++)
        {
            char c     = word[i];
            bool found = false;
            foreach (DawgEdge edge in Siblings(node))
            {
                if (edge.Letter != c) continue;
                if (i == word.Length - 1) return edge.IsEndOfWord;
                if (edge.NextIndex == 0)  return false;   // leaf, can't go deeper
                node  = edge.NextIndex;
                found = true;
                break;
            }
            if (!found) return false;
        }
        return false;
    }

    /// <summary>
    /// Pattern search. Supports:
    ///   ?  — any single letter (positional)
    ///   *  — zero or more letters (at most one star per pattern)
    /// Without wildcards, behaves like Contains but returns a list.
    /// </summary>
    public List<string> Match(string pattern)
    {
        pattern = pattern.ToLowerInvariant();
        int starPos = pattern.IndexOf('*');
        return starPos == -1
            ? PositionalMatch(pattern)
            : StarMatch(pattern[..starPos], pattern[(starPos + 1)..]);
    }

    /// <summary>
    /// Anagram search. <paramref name="letters"/> is the letter pool;
    /// use ? for a wildcard tile. Word length equals letters.Length.
    /// Example: "love?" finds all 5-letter words using l,o,v,e + any one letter.
    /// </summary>
    public List<string> Anagram(string letters)
    {
        letters = letters.ToLowerInvariant();

        int[] bag      = new int[26];
        int   wildcards = 0;
        foreach (char c in letters)
        {
            if (c == '?') wildcards++;
            else          bag[c - 'a']++;
        }

        var results = new List<string>();
        AnagramRecurse(0, bag, wildcards, letters.Length, 0, new char[letters.Length], results);
        return results;
    }

    // -------------------------------------------------------------------------
    // Core helpers
    // -------------------------------------------------------------------------

    private DawgEdge GetEdge(uint index) => new(_data[index]);

    /// <summary>Iterates the sibling list starting at <paramref name="nodeIndex"/>.</summary>
    private IEnumerable<DawgEdge> Siblings(uint nodeIndex)
    {
        uint i = nodeIndex;
        while (true)
        {
            DawgEdge edge = GetEdge(i++);
            yield return edge;
            if (edge.IsEndOfNode) yield break;
        }
    }

    // -------------------------------------------------------------------------
    // Positional match  (no star)
    // -------------------------------------------------------------------------

    private List<string> PositionalMatch(string pattern)
    {
        var results = new List<string>();
        PositionalRecurse(0, pattern, 0, new char[pattern.Length], results);
        return results;
    }

    private void PositionalRecurse(uint node, string pattern, int depth, char[] buf, List<string> results)
    {
        char pat    = pattern[depth];
        bool isLast = depth == pattern.Length - 1;

        foreach (DawgEdge edge in Siblings(node))
        {
            if (pat != '?' && pat != edge.Letter) continue;

            buf[depth] = edge.Letter;

            if (isLast)
            {
                if (edge.IsEndOfWord) results.Add(new string(buf));
            }
            else if (edge.NextIndex != 0)
            {
                PositionalRecurse(edge.NextIndex, pattern, depth + 1, buf, results);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Star match  (one * wildcard)
    // -------------------------------------------------------------------------

    private List<string> StarMatch(string prefix, string suffix)
    {
        var  results      = new List<string>();
        uint childNode    = 0;
        bool prefixIsWord = false;

        if (prefix.Length > 0)
        {
            if (!TryNavigate(prefix, out childNode, out prefixIsWord))
                return results;
        }

        // Include the prefix itself if it is a word and matches the suffix constraint.
        if (prefixIsWord && HasSuffix(prefix, suffix))
            results.Add(prefix);

        // Enumerate everything in the subtree, filtering by suffix.
        // Guard: childNode == 0 with a non-empty prefix means the prefix node is a leaf.
        if (childNode > 0 || prefix.Length == 0)
        {
            var current = new List<char>(prefix);
            EnumerateSubtree(childNode, current, suffix, results);
        }

        return results;
    }

    /// <summary>
    /// Walks the DAWG along <paramref name="prefix"/>.
    /// Returns false if any letter is missing. On success, <paramref name="childNode"/>
    /// is the first-sibling index of the last letter's children (0 = leaf),
    /// and <paramref name="isWord"/> reflects the End-of-Word flag on the last edge.
    /// </summary>
    private bool TryNavigate(string prefix, out uint childNode, out bool isWord)
    {
        childNode = 0;
        isWord    = false;
        uint node = 0;

        for (int i = 0; i < prefix.Length; i++)
        {
            char c     = prefix[i];
            bool found = false;
            foreach (DawgEdge edge in Siblings(node))
            {
                if (edge.Letter != c) continue;
                if (i == prefix.Length - 1)
                {
                    isWord    = edge.IsEndOfWord;
                    childNode = edge.NextIndex;
                }
                else
                {
                    if (edge.NextIndex == 0) return false;  // prefix too long, hit a leaf
                    node = edge.NextIndex;
                }
                found = true;
                break;
            }
            if (!found) return false;
        }
        return true;
    }

    private void EnumerateSubtree(uint node, List<char> current, string suffix, List<string> results)
    {
        foreach (DawgEdge edge in Siblings(node))
        {
            current.Add(edge.Letter);

            if (edge.IsEndOfWord && HasSuffix(current, suffix))
                results.Add(new string(current.ToArray()));

            if (edge.NextIndex != 0)
                EnumerateSubtree(edge.NextIndex, current, suffix, results);

            current.RemoveAt(current.Count - 1);
        }
    }

    private static bool HasSuffix(List<char> chars, string suffix)
    {
        if (suffix.Length == 0)            return true;
        if (chars.Count < suffix.Length)   return false;
        int offset = chars.Count - suffix.Length;
        for (int i = 0; i < suffix.Length; i++)
            if (chars[offset + i] != suffix[i]) return false;
        return true;
    }

    private static bool HasSuffix(string s, string suffix) =>
        suffix.Length == 0 || s.EndsWith(suffix, StringComparison.Ordinal);

    // -------------------------------------------------------------------------
    // Anagram match
    // -------------------------------------------------------------------------

    private void AnagramRecurse(
        uint node, int[] bag, int wildcards,
        int targetLen, int depth, char[] buf, List<string> results)
    {
        bool isLast = depth == targetLen - 1;

        foreach (DawgEdge edge in Siblings(node))
        {
            int  li       = edge.Letter - 'a';
            bool usedBag  = false;
            bool usedWild = false;

            if (bag[li] > 0)       { bag[li]--; usedBag  = true; }
            else if (wildcards > 0) { wildcards--; usedWild = true; }
            else continue;

            buf[depth] = edge.Letter;

            if (isLast)
            {
                if (edge.IsEndOfWord) results.Add(new string(buf));
            }
            else if (edge.NextIndex != 0)
            {
                AnagramRecurse(edge.NextIndex, bag, wildcards, targetLen, depth + 1, buf, results);
            }

            if (usedBag)  bag[li]++;
            if (usedWild) wildcards++;
        }
    }
}
