namespace NeuralSwitch.Core.Model;

/// <summary>
/// Looks for NVIDIA's Neural Rendering model where it legitimately turns up: the folder NVIDIA's driver
/// downloads models into, NVIDIA's driver store, and the folders of games that ship DLSS 5. Nothing is
/// ever downloaded.
/// </summary>
public static class ModelLocator
{
    private const string DownloadedModel = "*.bin";

    private static string NgxModels =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NVIDIA", "NGX", "models");

    public static IReadOnlyList<ModelInfo> Find(IEnumerable<string> gameFolders) => Find(gameFolders, NgxModels);

    internal static IReadOnlyList<ModelInfo> Find(IEnumerable<string> gameFolders, string ngxModels)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new List<ModelInfo>();

        foreach (var path in Candidates(gameFolders, ngxModels))
        {
            if (seen.Add(System.IO.Path.GetFullPath(path)) && ModelInfo.Inspect(path) is { } model && Accept(model))
                found.Add(model);
        }

        // Signed copies first, newest first.
        return [.. found.OrderByDescending(m => m.IsSignedByNvidia).ThenByDescending(m => ParseVersion(m.Version))];
    }

    /// <summary>
    /// A file named nvngx_dlssnr.dll is the model, signed or not (an unsigned one is listed so the user can be
    /// warned about it). A downloaded model's generic .bin name says nothing about it, so only NVIDIA's
    /// signature makes it one.
    /// </summary>
    internal static bool Accept(ModelInfo model) =>
        !model.Path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) || model.IsSignedByNvidia;

    private static IEnumerable<string> Candidates(IEnumerable<string> gameFolders, string ngxModels)
    {
        // NVIDIA's driver downloads a model the first time a game asks for it, into
        // models\<feature>\versions\<id>\files\, as a signed DLL under a generic name (160_E658700.bin),
        // the way DLSS's own dlss, dlssd and dlssg models arrive. Neural Rendering is the dlssnr feature:
        // the driver's NGX core knows it by that name.
        foreach (var file in SafeFiles(System.IO.Path.Combine(ngxModels, "dlssnr"), DownloadedModel, 4))
            yield return file;

        foreach (var file in SafeFiles(ngxModels, ModelInfo.FileName, 6))
            yield return file;

        // The display driver's own folder: nv_dispi.inf on desktops, other names on laptops (nvlti.inf, ...).
        // Driver 621.01 does not carry the model here, but a later one might.
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var driverStore = System.IO.Path.Combine(windows, "System32", "DriverStore", "FileRepository");

        foreach (var dir in SafeDirectories(driverStore, "nv*.inf_amd64_*"))
            foreach (var file in SafeFiles(dir, ModelInfo.FileName, 2))
                yield return file;

        // Where RenoDX's installer (RHI) keeps it.
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        foreach (var file in SafeFiles(System.IO.Path.Combine(localAppData, "RHI", "DLSS-NR"), ModelInfo.FileName, 1))
            yield return file;

        foreach (var folder in gameFolders)
            foreach (var file in SafeFiles(folder, ModelInfo.FileName, 8))
                yield return file;
    }

    private static Version ParseVersion(string? text) => Version.TryParse(text, out var v) ? v : new Version(0, 0);

    private static IEnumerable<string> SafeFiles(string root, string pattern, int depth)
    {
        if (!Directory.Exists(root))
            return [];

        try
        {
            return Directory.EnumerateFiles(root, pattern, new EnumerationOptions
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
