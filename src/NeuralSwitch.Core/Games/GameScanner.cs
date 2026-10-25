using System.Diagnostics;

namespace NeuralSwitch.Core.Games;

/// <summary>Finds games that ship DLSS: every folder in a library folder that has an nvngx_dlss.dll somewhere inside.</summary>
public static class GameScanner
{
    private const string DlssFile = "nvngx_dlss.dll";

    // Unreal keeps DLSS seven folders down (Engine\Plugins\...\Binaries\ThirdParty\Win64).
    private const int DlssSearchDepth = 8;

    // Unreal keeps its real executable three folders down (<Game>\Binaries\Win64).
    private const int ExeSearchDepth = 3;

    private static readonly string[] SkippedFolders =
    [
        "redist", "redistributables", "commonredist", "support", "installer", "installers", "directx", "vcredist",
        "dotnet", "prerequisites", "easyanticheat", "easyanticheat_eos", "battleye", "eaanticheat",
    ];

    private static readonly string[] SkippedExeWords =
    [
        "unins", "crash", "report", "setup", "redist", "install", "helper", "launcher", "updater", "dxweb",
        "anticheat", "start_protected_game", "beservice",
    ];

    private static readonly string[] AntiCheatFolders =
        ["EasyAntiCheat", "EasyAntiCheat_EOS", "BattlEye", "EAAntiCheat", "AntiCheatExpert", "GameGuard", "XIGNCODE"];

    private static readonly string[] AntiCheatFiles =
    [
        "EasyAntiCheat_EOS_Setup.exe", "EasyAntiCheat_Setup.exe", "start_protected_game.exe", "BEService.exe",
        "BEService_x64.exe", "BEClient_x64.dll", "EAAntiCheat.Installer.exe", "EAAntiCheat.GameServiceLauncher.exe",
    ];

    public static IReadOnlyList<Game> Scan(IEnumerable<string> libraryFolders)
    {
        var games = new List<Game>();

        foreach (var library in libraryFolders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var folder in SafeDirectories(library))
            {
                if (Inspect(folder) is { } game)
                    games.Add(game);
            }
        }

        return [.. games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>The game in this folder, or null when it ships no DLSS or has no executable to load beside.</summary>
    public static Game? Inspect(string gameRoot)
    {
        var dlss = Files(gameRoot, DlssFile, DlssSearchDepth).FirstOrDefault();

        if (dlss is null || FindMainExe(gameRoot) is not { } exe)
            return null;

        return new Game(Path.GetFileName(gameRoot.TrimEnd(Path.DirectorySeparatorChar)), gameRoot, exe, FileVersion(dlss),
                        DetectAntiCheat(gameRoot));
    }

    /// <summary>The game's own executable: the largest one that is not an installer, a crash reporter or a launcher.</summary>
    internal static string? FindMainExe(string gameRoot) =>
        Files(gameRoot, "*.exe", ExeSearchDepth)
            .Where(path => !IsSkipped(gameRoot, path))
            .Select(path => new FileInfo(path))
            .OrderByDescending(f => f.Length)
            .FirstOrDefault()?.FullName;

    internal static IReadOnlyList<string> DetectAntiCheat(string gameRoot)
    {
        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in Directories(gameRoot, ExeSearchDepth))
        {
            var name = Path.GetFileName(dir);
            var match = AntiCheatFolders.FirstOrDefault(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
                found.Add(match.StartsWith("EasyAntiCheat", StringComparison.Ordinal) ? "EasyAntiCheat" : match);
        }

        foreach (var file in Files(gameRoot, "*", ExeSearchDepth))
        {
            var name = Path.GetFileName(file);

            if (AntiCheatFiles.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase)))
                found.Add(name.StartsWith("BE", StringComparison.OrdinalIgnoreCase) ? "BattlEye" :
                          name.StartsWith("EAAntiCheat", StringComparison.OrdinalIgnoreCase) ? "EA AntiCheat" : "EasyAntiCheat");
        }

        return [.. found];
    }

    private static bool IsSkipped(string gameRoot, string exePath)
    {
        var relative = Path.GetRelativePath(gameRoot, exePath);
        var folders = relative.Split(Path.DirectorySeparatorChar)[..^1];

        // "_Redist", "_CommonRedist", "_Update to v1.131.0": folders an installer left behind.
        if (folders.Any(f => f.StartsWith('_') || SkippedFolders.Contains(f, StringComparer.OrdinalIgnoreCase)))
            return true;

        var file = Path.GetFileNameWithoutExtension(exePath);
        return SkippedExeWords.Any(w => file.Contains(w, StringComparison.OrdinalIgnoreCase));
    }

    internal static string? FileVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}.{info.FilePrivatePart}";
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static EnumerationOptions Options(int depth) => new()
    {
        RecurseSubdirectories = true,
        MaxRecursionDepth = depth,
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.CaseInsensitive,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    private static IEnumerable<string> Files(string root, string pattern, int depth)
    {
        try
        {
            return Directory.EnumerateFiles(root, pattern, Options(depth)).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<string> Directories(string root, int depth)
    {
        try
        {
            return Directory.EnumerateDirectories(root, "*", Options(depth)).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeDirectories(string root)
    {
        try
        {
            return Directory.GetDirectories(root);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
