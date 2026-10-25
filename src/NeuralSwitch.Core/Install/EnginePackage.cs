namespace NeuralSwitch.Core.Install;

/// <summary>
/// The patched engine as engine\build-engine.ps1 stages it: OptiScaler.dll, the forwarder
/// nvngx.dll_dlssnr.dll and the shipped OptiScaler.ini. A release puts it in an "engine" folder beside the app.
/// </summary>
public sealed class EnginePackage
{
    public const string EngineDll = "OptiScaler.dll";
    public const string Forwarder = "nvngx.dll_dlssnr.dll";
    public const string Ini = "OptiScaler.ini";

    private EnginePackage(string directory) => Directory = directory;

    public string Directory { get; }
    public string EngineDllPath => Path.Combine(Directory, EngineDll);
    public string ForwarderPath => Path.Combine(Directory, Forwarder);
    public string IniPath => Path.Combine(Directory, Ini);

    public static bool IsComplete(string directory) =>
        File.Exists(Path.Combine(directory, EngineDll)) && File.Exists(Path.Combine(directory, Forwarder)) &&
        File.Exists(Path.Combine(directory, Ini));

    public static EnginePackage? Open(string directory) => IsComplete(directory) ? new EnginePackage(directory) : null;

    /// <summary>The configured folder if given, else "engine" beside the app, else engine\dist of the source tree it was built from.</summary>
    public static EnginePackage? Find(string? configured = null)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return Open(configured);

        var appDir = AppContext.BaseDirectory;

        if (Open(Path.Combine(appDir, "engine")) is { } beside)
            return beside;

        for (var dir = new DirectoryInfo(appDir); dir is not null; dir = dir.Parent)
        {
            if (Open(Path.Combine(dir.FullName, "engine", "dist")) is { } built)
                return built;
        }

        return null;
    }
}
