using WordScan.Dawg;

string dawgPath = Path.Combine(AppContext.BaseDirectory, "dawg.bin");

Console.WriteLine("WordScan — DAWG Dictionary Explorer");
Console.Write("Loading...");
DawgDictionary dawg = DawgDictionary.Load(dawgPath);
Console.WriteLine($" {dawg.EdgeCount:N0} edges loaded.");
Console.WriteLine();
Console.WriteLine("Pattern syntax:");
Console.WriteLine("  to???      exactly 5 letters, starting with 'to'");
Console.WriteLine("  ???er      exactly 5 letters, ending with 'er'");
Console.WriteLine("  to*        all words starting with 'to'");
Console.WriteLine("  *er        all words ending with 'er'");
Console.WriteLine("  to*er      starts with 'to' and ends with 'er'");
Console.WriteLine("  {love?}    5-letter anagram: l,o,v,e + 1 wildcard tile");
Console.WriteLine("  cat        exact lookup");
Console.WriteLine("  q / quit   exit");
Console.WriteLine();

while (true)
{
    Console.Write("Pattern> ");
    string? input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input)) continue;
    if (input is "q" or "quit") break;

    List<string> results = input.StartsWith('{') && input.EndsWith('}')
        ? dawg.Anagram(input[1..^1])
        : dawg.Match(input);

    if (results.Count == 0)
    {
        Console.WriteLine("  (no matches)");
    }
    else
    {
        PrintColumns(results);
        Console.WriteLine($"  {results.Count} match{(results.Count == 1 ? "" : "es")}");
    }

    Console.WriteLine();
}

static void PrintColumns(List<string> words)
{
    int colWidth    = words.Max(w => w.Length) + 2;
    int windowWidth = Console.IsOutputRedirected ? 80 : Console.WindowWidth;
    int cols        = Math.Max(1, Math.Min(6, windowWidth / colWidth));
    int col         = 0;
    foreach (string word in words)
    {
        Console.Write("  " + word.PadRight(colWidth - 2));
        if (++col == cols) { Console.WriteLine(); col = 0; }
    }
    if (col != 0) Console.WriteLine();
}
