using System.Collections.Concurrent;

namespace FireAndSteel.Server.Tests;

internal sealed class InMemoryLogger
{
    private readonly ConcurrentQueue<string> _lines = new();

    public Action<string> Sink => msg => _lines.Enqueue(msg);

    public int CountContaining(string needle)
    {
        if (string.IsNullOrEmpty(needle))
            throw new ArgumentException("needle must not be empty", nameof(needle));

        var count = 0;
        foreach (var l in _lines)
        {
            if (l.Contains(needle, StringComparison.Ordinal))
                count++;
        }
        return count;
    }

    public string Dump()
        => string.Join(Environment.NewLine, _lines);
}
