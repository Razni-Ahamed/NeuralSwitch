using System.Text.Json;

namespace NeuralSwitch.Core.Install;

/// <summary>
/// What an install put into a game folder, kept beside it as neuralswitch.installed.json, so removing it
/// takes away exactly those files and nothing the game or another mod owns.
/// </summary>
public sealed record InstallRecord
{
    public const string FileName = "neuralswitch.installed.json";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public int Version { get; init; } = 1;

    /// <summary>The name the engine loads under: winmm.dll, version.dll, ... or OptiScaler.asi beside an ASI loader.</summary>
    public string ProxyName { get; init; } = "";

    /// <summary>Each file installed, with its SHA-256. The ini is "config": the engine rewrites it when settings are saved.</summary>
    public Dictionary<string, string> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public DateTime InstalledUtc { get; init; }
    public string? EngineCommit { get; init; }

    public static InstallRecord? Load(string gameExeDirectory)
    {
        var path = Path.Combine(gameExeDirectory, FileName);

        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<InstallRecord>(File.ReadAllText(path), Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(string gameExeDirectory) =>
        File.WriteAllText(Path.Combine(gameExeDirectory, FileName), JsonSerializer.Serialize(this, Json));
}
