using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralSwitch.Core.Engine;

namespace NeuralSwitch.Core;

/// <summary>What the app remembers between runs, in %APPDATA%\NeuralSwitch\settings.json.</summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string FilePath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NeuralSwitch", "settings.json");

    /// <summary>Folders whose subfolders are games.</summary>
    public List<string> LibraryFolders { get; set; } = [];

    /// <summary>The nvngx_dlssnr.dll the user picked, when the automatic search finds none.</summary>
    public string? ModelPath { get; set; }

    /// <summary>A built engine somewhere other than beside the app.</summary>
    public string? EngineDirectory { get; set; }

    /// <summary>The style a newly prepared game starts with.</summary>
    public NrStyle DefaultStyle { get; set; } = NrStyle.Natural;

    /// <summary>The letter for Ctrl+Alt+letter that turns Neural Rendering on and off from inside a game.</summary>
    public char ToggleHotkey { get; set; } = 'N';

    /// <summary>The letter for Ctrl+Alt+letter that moves to the next style.</summary>
    public char StyleHotkey { get; set; } = 'B';

    /// <summary>The version of USER_CAUTION.md the user accepted on first run. 0: not yet.</summary>
    public int AcceptedCaution { get; set; }

    /// <summary>Closing the window keeps NeuralSwitch in the notification area, so its hotkeys keep working.</summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>Whether the "still running in the notification area" hint has been shown once.</summary>
    public bool TrayHintShown { get; set; }

    public static AppSettings Load(string? path = null)
    {
        path ??= FilePath;

        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) is { } loaded)
                return loaded;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A broken settings file is replaced by the defaults rather than stopping the app.
        }

        var defaults = new AppSettings();

        if (Directory.Exists(@"C:\Games"))
            defaults.LibraryFolders.Add(@"C:\Games");

        return defaults;
    }

    public void Save(string? path = null)
    {
        path ??= FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
    }
}
