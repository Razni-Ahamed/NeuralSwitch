using NeuralSwitch.Core.Install;

namespace NeuralSwitch.Tests;

public sealed class IniEditorTests
{
    private const string Template =
        "[Upscalers]\r\n; Select Upscaler for Dx12 games\r\nDx12Upscaler=auto\r\n\r\n" +
        "[DlssNr]\r\nToggleKey=auto\r\n; Model style: 0 Default, 1 Natural\r\nStyle=auto\r\nEnabled=auto\r\n\r\n" +
        "[Log]\r\nLogLevel=auto\r\n";

    private static Dictionary<string, IReadOnlyDictionary<string, string>> Settings(params (string Section, string Key, string Value)[] items) =>
        items.GroupBy(i => i.Section)
             .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, string>)g.ToDictionary(i => i.Key, i => i.Value));

    [Fact]
    public void Replaces_listed_keys_and_keeps_comments()
    {
        var ini = IniEditor.Apply(Template, Settings(("DlssNr", "Style", "1"), ("DlssNr", "Enabled", "false")));

        Assert.Contains("; Model style: 0 Default, 1 Natural\r\nStyle=1\r\nEnabled=false\r\n", ini);
        Assert.Contains("ToggleKey=auto", ini);
        Assert.Contains("Dx12Upscaler=auto", ini);
    }

    [Fact]
    public void Adds_unlisted_keys_at_the_end_of_their_section()
    {
        var ini = IniEditor.Apply(Template, Settings(("DlssNr", "RemoteControl", "true"), ("Upscalers", "Dx11Upscaler", "dlss_12")));

        Assert.Equal("true", IniEditor.Read(ini, "DlssNr", "RemoteControl"));
        Assert.Equal("dlss_12", IniEditor.Read(ini, "Upscalers", "Dx11Upscaler"));
        Assert.True(ini.IndexOf("RemoteControl=true", StringComparison.Ordinal) < ini.IndexOf("[Log]", StringComparison.Ordinal));
    }

    [Fact]
    public void Adds_a_missing_section_at_the_end()
    {
        var ini = IniEditor.Apply(Template, Settings(("Menu", "ShortcutKey", "45")));

        Assert.EndsWith("[Menu]\r\nShortcutKey=45\r\n", ini);
    }

    [Fact]
    public void Writes_crlf_and_leaves_lf_templates_working()
    {
        var ini = IniEditor.Apply(Template.Replace("\r\n", "\n"), Settings(("Log", "LogLevel", "2")));

        Assert.DoesNotContain("\r\r", ini);
        Assert.Equal(ini.Split('\n').Length - 1, ini.Split("\r\n").Length - 1);
        Assert.Equal("2", IniEditor.Read(ini, "Log", "LogLevel"));
    }

    [Fact]
    public void Section_and_key_names_match_without_case()
    {
        var ini = IniEditor.Apply(Template, Settings(("dlssnr", "style", "2")));

        Assert.Equal("2", IniEditor.Read(ini, "DlssNr", "Style"));
        Assert.Single(ini.Split("\r\n"), l => l.StartsWith("Style=", StringComparison.OrdinalIgnoreCase));
    }
}
