namespace NeuralSwitch.Core.Games;

/// <summary>An installed game that ships DLSS, found in one of the library folders.</summary>
/// <param name="Name">The game's folder name.</param>
/// <param name="Root">The game's top folder.</param>
/// <param name="ExePath">The game's own executable. The engine goes beside it.</param>
/// <param name="DlssVersion">The version of the game's nvngx_dlss.dll, as Windows reports it.</param>
/// <param name="AntiCheat">Anti-cheat software found in the game's folders. Installing is refused when any is.</param>
public sealed record Game(string Name, string Root, string ExePath, string? DlssVersion, IReadOnlyList<string> AntiCheat)
{
    public string ExeDirectory => Path.GetDirectoryName(ExePath)!;
    public string ExeName => Path.GetFileName(ExePath);
    public bool HasAntiCheat => AntiCheat.Count > 0;
}
