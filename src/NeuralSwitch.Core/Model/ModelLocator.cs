namespace NeuralSwitch.Core.Model;

/// <summary>
/// Looks for nvngx_dlssnr.dll where it legitimately turns up: NVIDIA's driver store, NVIDIA's own model
/// download folder, and the folders of games that ship DLSS 5. Nothing is ever downloaded.
/// </summary>
public static class ModelLocator
{
    public static IReadOnlyList<ModelInfo> Find(IEnumerable<string> gameFolders)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new List<ModelInfo>();

        foreach (var path in Candidates(gameFolders))
        {
            if (seen.Add(System.IO.Path.GetFullPath(path)) && ModelInfo.Inspect(path) is { } model)
                found.Add(model);
        }

        // Signed copies first, newest first.
        return [.. found.OrderByDescending(m => m.IsSignedByNvidia).ThenByDescending(m => ParseVersion(m.Version))];
    }

    private static IEnumerable<string> Candidates(IEnumerable<string> gameFolders)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // The display driver's own folder: nv_dispi.inf on desktops, other names on laptops (nvlti.inf, ...).
        var driverStore = System.IO.Path.Combine(windows, "System32", "DriverStore", "FileRepository");

        foreach (var dir in SafeDirectories(driverStore, "nv*.inf_amd64_*"))
            foreach (var file in SafeFiles(dir, 2))
                yield return file;

        foreach (var file in SafeFiles(System.IO.Path.Combine(programData, "NVIDIA", "NGX", "models"), 6))
            yield return file;

        // Where RenoDX's installer (RHI) keeps it.
        foreach (var file in SafeFiles(System.IO.Path.Combine(localAppData, "RHI", "DLSS-NR"), 1))
            yield return file;

        foreach (var folder in gameFolders)
            foreach (var file in SafeFiles(folder, 8))
                yield return file;
    }

    private static Version ParseVersion(string? text) => Version.TryParse(text, out var v) ? v : new Version(0, 0);

    private static IEnumerable<string> SafeFiles(string root, int depth)
    {
        if (!Directory.Exists(root))
            return [];

        try
        {
            return Directory.EnumerateFiles(root, ModelInfo.FileName, new EnumerationOptions
            {
                RecurseSubdirectories = true,
                MaxRecursionDepth = depth,
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.CaseInsensitive,
                AttributesToSkip = FileAttributes.ReparsePoint,
            }).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeDirectories(string root, string pattern)
    {
        if (!Directory.Exists(root))
            return [];

        try
        {
            return Directory.GetDirectories(root, pattern);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
