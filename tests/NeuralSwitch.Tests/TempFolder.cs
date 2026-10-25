namespace NeuralSwitch.Tests;

/// <summary>A folder under %TEMP% that goes away after the test.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder() => Directory.CreateDirectory(Path);

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "neuralswitch-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Creates a file of the given size (and its folders), relative to this folder.</summary>
    public string File(string relative, long size = 16, string? text = null)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);

        if (text is not null)
        {
            System.IO.File.WriteAllText(full, text);
        }
        else
        {
            using var stream = System.IO.File.Create(full);
            stream.SetLength(size);
        }

        return full;
    }

    public string Folder(string relative) => Directory.CreateDirectory(System.IO.Path.Combine(Path, relative)).FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
