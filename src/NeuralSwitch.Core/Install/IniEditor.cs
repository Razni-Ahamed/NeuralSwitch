using System.Text;
using System.Text.RegularExpressions;

namespace NeuralSwitch.Core.Install;

/// <summary>
/// Sets keys in the engine's shipped OptiScaler.ini while keeping every comment and every other key as it
/// is, the way the engine's own deploy/install.py does. A key the shipped file does not list goes at the
/// end of its section; a section it does not have goes at the end of the file.
/// </summary>
public static partial class IniEditor
{
    public static string Apply(string template, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> settings)
    {
        var output = new List<string>();
        var written = new HashSet<(string Section, string Key)>();
        var seenSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? section = null;

        void AddMissing(string? current)
        {
            if (current is null || !settings.TryGetValue(current, out var keys))
                return;

            foreach (var (key, value) in keys)
            {
                if (written.Add((current, key)))
                    output.Add($"{key}={value}");
            }
        }

        foreach (var raw in template.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw;

            if (SectionLine().Match(line) is { Success: true } header)
            {
                AddMissing(section);
                section = settings.Keys.FirstOrDefault(k => k.Equals(header.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                          ?? header.Groups[1].Value;
                seenSections.Add(section);
            }
            else if (section is not null && settings.TryGetValue(section, out var keys) &&
                     KeyLine().Match(line) is { Success: true } kv &&
                     keys.FirstOrDefault(k => k.Key.Equals(kv.Groups[1].Value, StringComparison.OrdinalIgnoreCase)) is { Key: not null } setting)
            {
                // Only the first line of a key is replaced; the engine reads the first one anyway.
                if (written.Add((section, setting.Key)))
                    line = $"{kv.Groups[1].Value}={setting.Value}";
            }

            output.Add(line);
        }

        AddMissing(section);

        foreach (var (name, keys) in settings.Where(s => !seenSections.Contains(s.Key)))
        {
            output.Add($"[{name}]");
            AddMissing(name);
        }

        // The template ends with a newline, which leaves an empty last element; keep exactly one.
        while (output.Count > 0 && output[^1].Length == 0)
            output.RemoveAt(output.Count - 1);

        return new StringBuilder().AppendJoin("\r\n", output).Append("\r\n").ToString();
    }

    /// <summary>The value of one key, or null. For reading back what an install wrote.</summary>
    public static string? Read(string ini, string section, string key)
    {
        string? current = null;

        foreach (var line in ini.Replace("\r\n", "\n").Split('\n'))
        {
            if (SectionLine().Match(line) is { Success: true } header)
                current = header.Groups[1].Value;
            else if (string.Equals(current, section, StringComparison.OrdinalIgnoreCase) &&
                     KeyLine().Match(line) is { Success: true } kv &&
                     kv.Groups[1].Value.Equals(key, StringComparison.OrdinalIgnoreCase))
                return line[(line.IndexOf('=') + 1)..].Trim();
        }

        return null;
    }

    [GeneratedRegex(@"^\[(.+)\]\s*$")]
    private static partial Regex SectionLine();

    [GeneratedRegex(@"^([A-Za-z0-9_]+)=")]
    private static partial Regex KeyLine();
}
