using System.Globalization;

namespace NeuralSwitch.Core.Engine;

/// <summary>Finds running games that have the patched engine loaded, by the pipes they have open.</summary>
public static class EngineDiscovery
{
    private const string PipeRoot = @"\\.\pipe\";

    /// <summary>Process ids of games whose engine is listening, lowest first.</summary>
    public static IReadOnlyList<int> FindRunning(Func<IEnumerable<string>>? listPipes = null)
    {
        var pids = new SortedSet<int>();

        foreach (var entry in (listPipes ?? ListPipes)())
        {
            var name = entry.StartsWith(PipeRoot, StringComparison.OrdinalIgnoreCase) ? entry[PipeRoot.Length..] : entry;

            if (name.StartsWith(EngineClient.PipePrefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(name.AsSpan(EngineClient.PipePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
            {
                pids.Add(pid);
            }
        }

        return [.. pids];
    }

    private static IEnumerable<string> ListPipes()
    {
        try
        {
            return Directory.GetFiles(PipeRoot);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Some pipes carry names the file APIs reject; one of those must not hide the games.
            return [];
        }
    }
}
