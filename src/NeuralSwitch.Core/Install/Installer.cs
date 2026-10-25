using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NeuralSwitch.Core.Engine;
using NeuralSwitch.Core.Games;
using NeuralSwitch.Core.Model;

namespace NeuralSwitch.Core.Install;

public enum InstallState
{
    /// <summary>Nothing of ours or the engine's in the game folder.</summary>
    NotInstalled,

    /// <summary>Installed by NeuralSwitch.</summary>
    Installed,

    /// <summary>OptiScaler is there, put in by something else. Left alone.</summary>
    OtherOptiScaler,
}

public sealed class InstallBlockedException(string message) : Exception(message);

public sealed record InstallOptions
{
    /// <summary>The style the engine starts with. The app can change it while the game runs.</summary>
    public NrStyle Style { get; init; } = NrStyle.Natural;

    /// <summary>Whether Neural Rendering is on when the game starts. Off means the game starts exactly as before.</summary>
    public bool StartEnabled { get; init; }

    /// <summary>Accept a model that is not signed by NVIDIA. Only after the user has been told what that means.</summary>
    public bool AllowUnsignedModel { get; init; }

    /// <summary>Install the engine alone, for checking a game before NVIDIA's model is available: it loads,
    /// connects and takes commands, and reports the model as missing once Neural Rendering is switched on.</summary>
    public bool WithoutModel { get; init; }
}

/// <summary>
/// Puts the patched engine into a game's folder and takes it out again, following the engine's own
/// deploy/install.py: a free proxy name, nothing overwritten that is not ours, a record of every file.
/// </summary>
public static class Installer
{
    // Names Windows lets a game load from its own folder before System32, tried in this order.
    private static readonly string[] ProxyNames = ["winmm.dll", "version.dll", "dbghelp.dll", "wininet.dll", "winhttp.dll"];

    private const string AsiName = "OptiScaler.asi";

    // Written by the engine while it runs; they go when it goes.
    private static readonly string[] EngineLeftovers = ["OptiScaler.log"];
    private static readonly string[] EngineLeftoverFolders = ["dlssnr-capture", "dlssnr-cachedump"];

    public static InstallState GetState(Game game)
    {
        if (InstallRecord.Load(game.ExeDirectory) is not null)
            return InstallState.Installed;

        return File.Exists(Path.Combine(game.ExeDirectory, EnginePackage.Ini)) ? InstallState.OtherOptiScaler : InstallState.NotInstalled;
    }

    /// <summary>The settings an install writes. The quality preset is the engine's own (deploy/install.py).</summary>
    internal static Dictionary<string, IReadOnlyDictionary<string, string>> Settings(InstallOptions options) => new()
    {
        ["Upscalers"] = new Dictionary<string, string>
        {
            ["Dx12Upscaler"] = "dlss",
            // The only DX11 route that can run Neural Rendering.
            ["Dx11Upscaler"] = "dlss_12",
            // Left at auto, the engine swaps a Vulkan game's DLSS for FSR 2.2 and runs the model over that.
            ["VulkanUpscaler"] = "dlss",
        },
        ["Log"] = new Dictionary<string, string> { ["LogToFile"] = "true", ["LogLevel"] = "2" },
        // The engine would check GitHub for newer OptiScaler releases at every start. A stock OptiScaler
        // must not replace this patched build, so the check has nothing useful to say; keep it offline.
        ["Hotfix"] = new Dictionary<string, string> { ["CheckForUpdate"] = "false" },
        ["DlssNr"] = new Dictionary<string, string>
        {
            ["Enabled"] = options.StartEnabled ? "true" : "false",
            ["Style"] = ((int)options.Style).ToString(CultureInfo.InvariantCulture),
            ["RemoteControl"] = "true",
            ["CacheEnabled"] = "true",
            ["CacheInterval"] = "1",
            ["PreSr"] = "true",
            ["WorkingScale"] = "0.67",
            ["JbuUpsample"] = "true",
            ["CacheRefreshBlend"] = "1.0",
            ["WhitePointSource"] = "3",
            // Otherwise the first run writes eight before/after frames into the game folder.
            ["AutoCapture"] = "false",
        },
    };

    /// <param name="modelPath">NVIDIA's nvngx_dlssnr.dll. Ignored, and may be null, with <see cref="InstallOptions.WithoutModel"/>.</param>
    public static InstallRecord Install(Game game, EnginePackage engine, string? modelPath, InstallOptions options)
    {
        var dir = game.ExeDirectory;

        if (game.HasAntiCheat)
            throw new InstallBlockedException(
                $"{game.Name} uses {string.Join(" and ", game.AntiCheat)}. Loading the engine into it can get your account banned, so NeuralSwitch will not install here.");

        ThrowIfRunning(game);

        if (!options.WithoutModel)
        {
            var model = (modelPath is null ? null : ModelInfo.Inspect(modelPath))
                        ?? throw new InstallBlockedException($"The NVIDIA model was not found at {modelPath}.");

            if (!model.IsSignedByNvidia && !options.AllowUnsignedModel)
                throw new InstallBlockedException("The model file is not signed by NVIDIA. It may have been modified.");
        }

        var previous = InstallRecord.Load(dir);

        if (previous is null && File.Exists(Path.Combine(dir, EnginePackage.Ini)))
            throw new InstallBlockedException("Another OptiScaler install is already in this game. Remove it first.");

        var proxy = previous?.ProxyName ?? PickProxyName(dir);
        var modelTarget = Path.Combine(dir, ModelInfo.FileName);

        // A copy of the model that was already here and is identical stays, and stays behind on removal.
        var keepModel = options.WithoutModel ||
                        (previous?.Files.ContainsKey(ModelInfo.FileName) != true && File.Exists(modelTarget) &&
                         Sha256(modelTarget) == Sha256(modelPath!));

        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [proxy] = engine.EngineDllPath,
            [EnginePackage.Forwarder] = engine.ForwarderPath,
        };

        if (!keepModel)
            sources[ModelInfo.FileName] = modelPath!;

        foreach (var name in sources.Keys.Append(EnginePackage.Ini))
        {
            if (File.Exists(Path.Combine(dir, name)) && previous?.Files.ContainsKey(name) != true)
                throw new InstallBlockedException($"{name} is already in the game folder and is not ours. Not overwriting it.");
        }

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var written = new List<string>();

        try
        {
            foreach (var (name, source) in sources)
            {
                var target = Path.Combine(dir, name);
                File.Copy(source, target, overwrite: true);
                written.Add(target);
                files[name] = Sha256(target);
            }

            var iniTarget = Path.Combine(dir, EnginePackage.Ini);
            File.WriteAllText(iniTarget, IniEditor.Apply(File.ReadAllText(engine.IniPath), Settings(options)), new UTF8Encoding(false));
            written.Add(iniTarget);
            files[EnginePackage.Ini] = "config";

            var record = new InstallRecord
            {
                ProxyName = proxy,
                Files = files,
                InstalledUtc = DateTime.UtcNow,
                EngineCommit = engine.Commit,
            };

            record.Save(dir);
            return record;
        }
        catch when (previous is null)
        {
            // A first install that fails halfway leaves the folder as it found it.
            foreach (var path in written)
                TryDelete(path);

            throw;
        }
    }

    public static void Uninstall(Game game)
    {
        var dir = game.ExeDirectory;
        var record = InstallRecord.Load(dir) ?? throw new InstallBlockedException("NeuralSwitch did not install anything in this game.");

        ThrowIfRunning(game);

        foreach (var name in record.Files.Keys)
            TryDelete(Path.Combine(dir, name));

        foreach (var name in EngineLeftovers)
            TryDelete(Path.Combine(dir, name));

        foreach (var name in EngineLeftoverFolders)
        {
            var folder = Path.Combine(dir, name);

            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }

        File.Delete(Path.Combine(dir, InstallRecord.FileName));
    }

    /// <summary>OptiScaler.asi beside an ASI loader, else the first proxy name not already taken.</summary>
    internal static string PickProxyName(string dir)
    {
        var hasAsiLoader = Directory.EnumerateFiles(dir, "*.asi").Any() ||
                           ProxyNames.Append("dinput8.dll").Append("d3d9.dll")
                                     .Select(n => Path.Combine(dir, n))
                                     .Any(p => File.Exists(p) && Contains(p, "Ultimate-ASI-Loader"));

        if (hasAsiLoader)
            return AsiName;

        return ProxyNames.FirstOrDefault(n => !File.Exists(Path.Combine(dir, n)))
               ?? throw new InstallBlockedException($"Every name the engine can load under is taken in this game ({string.Join(", ", ProxyNames)}).");
    }

    private static void ThrowIfRunning(Game game)
    {
        var name = Path.GetFileNameWithoutExtension(game.ExePath);
        var running = Process.GetProcessesByName(name);

        foreach (var p in running)
            p.Dispose();

        if (running.Length > 0)
            throw new InstallBlockedException($"Close {game.Name} first. Its files are in use while it runs.");
    }

    private static bool Contains(string path, string text)
    {
        var needle = Encoding.ASCII.GetBytes(text);
        return File.ReadAllBytes(path).AsSpan().IndexOf(needle) >= 0;
    }

    internal static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
