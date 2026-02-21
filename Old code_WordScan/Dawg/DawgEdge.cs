namespace WordScan.Dawg;

/// <summary>
/// Wraps a raw uint32 DAWG entry and exposes its fields by name.
/// Bit layout: [0-4] letter (1-26), [5] end-of-word, [6] end-of-node, [7-31] next index.
/// </summary>
public readonly struct DawgEdge
{
    private readonly uint _raw;

    public DawgEdge(uint raw) => _raw = raw;

    public char Letter      => (char)('a' + (_raw & 0x1F) - 1);
    public bool IsEndOfWord => ((_raw >> 5) & 1) == 1;
    public bool IsEndOfNode => ((_raw >> 6) & 1) == 1;
    public uint NextIndex   => (_raw >> 7) & 0x1FFFFFF;
}
