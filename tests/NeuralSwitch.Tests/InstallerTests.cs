using System.Runtime.InteropServices;
using NeuralSwitch.Core.Engine;
using NeuralSwitch.Core.Games;
using NeuralSwitch.Core.Install;
using NeuralSwitch.Core.Model;

namespace NeuralSwitch.Tests;

public sealed class InstallerTests : IDisposable
{
    private const string ShippedIni = "[Upscalers]\r\nDx12Upscaler=auto\r\n\r\n[DlssNr]\r\nEnabled=auto\r\nStyle=auto\r\n";

    private readonly TempFolder _temp = new();
    private readonly EnginePackage _engine;
    private readonly string _model;
    private readonly InstallOptions _testModel = new() { AllowUnsignedModel = true };

    public InstallerTests()
    {
        _temp.File(@"engine\OptiScaler.dll", text: "engine");
        _temp.File(@"engine\nvngx.dll_dlssnr.dll", text: "forwarder");
        _temp.File(@"engine\OptiScaler.ini", text: ShippedIni);
        _temp.File(@"engine\engine.json", text: "{\"commit\":\"fc702f8\"}");
        _engine = EnginePackage.Open(Path.Combine(_temp.Path, "engine"))!;
        _model = _temp.File(@"model\nvngx_dlssnr.dll", text: "model");
    }

    public void Dispose() => _temp.Dispose();

    private Game MakeGame(string name = "Game", params string[] antiCheat)
    {
        var exe = _temp.File($@"games\{name}\{name}-not-running-{Guid.NewGuid():N}.exe", 1000);
        return new Game(name, Path.GetDirectoryName(exe)!, exe, "310.0.0.0", antiCheat);
    }

    private static string[] FilesIn(Game game) =>
        [.. Directory.GetFiles(game.ExeDirectory).Select(Path.GetFileName).Order(StringComparer.OrdinalIgnoreCase)!];

    [Fact]
    public void Installs_the_four_files_and_a_record()
    {
        var game = MakeGame();

        var record = Installer.Install(game, _engine, _model, _testModel);

        Assert.Equal("winmm.dll", record.ProxyName);
        Assert.Equal("fc702f8", record.EngineCommit);
        Assert.Equal("engine", File.ReadAllText(Path.Combine(game.ExeDirectory, "winmm.dll")));
        Assert.Equal("forwarder", File.ReadAllText(Path.Combine(game.ExeDirectory, "nvngx.dll_dlssnr.dll")));
        Assert.Equal("model", File.ReadAllText(Path.Combine(game.ExeDirectory, "nvngx_dlssnr.dll")));
        Assert.Equal(InstallState.Installed, Installer.GetState(game));
        Assert.Equal(4, record.Files.Count);
        Assert.True(File.Exists(Path.Combine(game.ExeDirectory, InstallRecord.FileName)));
    }

    [Fact]
    public void Writes_the_ini_with_remote_control_on_and_the_chosen_style()
    {
        var game = MakeGame();

        Installer.Install(game, _engine, _model, _testModel with { Style = NrStyle.Cinematic });
        var ini = File.ReadAllText(Path.Combine(game.ExeDirectory, "OptiScaler.ini"));

        Assert.Equal("false", IniEditor.Read(ini, "DlssNr", "Enabled"));
        Assert.Equal("2", IniEditor.Read(ini, "DlssNr", "Style"));
        Assert.Equal("true", IniEditor.Read(ini, "DlssNr", "RemoteControl"));
        Assert.Equal("false", IniEditor.Read(ini, "DlssNr", "AutoCapture"));
        Assert.Equal("dlss", IniEditor.Read(ini, "Upscalers", "Dx12Upscaler"));
        Assert.Equal("dlss_12", IniEditor.Read(ini, "Upscalers", "Dx11Upscaler"));
        Assert.Equal("dlss", IniEditor.Read(ini, "Upscalers", "VulkanUpscaler"));
        Assert.Equal("false", IniEditor.Read(ini, "Hotfix", "CheckForUpdate"));
    }

    [Fact]
    public void Uninstall_puts_the_folder_back_exactly()
    {
        var game = MakeGame();
        File.WriteAllText(Path.Combine(game.ExeDirectory, "game-data.pak"), "game");
        var before = FilesIn(game);

        Installer.Install(game, _engine, _model, _testModel);
        File.WriteAllText(Path.Combine(game.ExeDirectory, "OptiScaler.log"), "written by the engine");
        Directory.CreateDirectory(Path.Combine(game.ExeDirectory, "dlssnr-capture"));
        Installer.Uninstall(game);

        Assert.Equal(before, FilesIn(game));
        Assert.False(Directory.Exists(Path.Combine(game.ExeDirectory, "dlssnr-capture")));
        Assert.Equal(InstallState.NotInstalled, Installer.GetState(game));
    }

    [Fact]
    public void Takes_the_next_free_name_when_the_game_has_its_own_winmm()
    {
        var game = MakeGame();
        File.WriteAllText(Path.Combine(game.ExeDirectory, "winmm.dll"), "the game's own");

        var record = Installer.Install(game, _engine, _model, _testModel);
        Installer.Uninstall(game);

        Assert.Equal("version.dll", record.ProxyName);
        Assert.Equal("the game's own", File.ReadAllText(Path.Combine(game.ExeDirectory, "winmm.dll")));
    }

    [Fact]
    public void Loads_as_an_asi_beside_an_asi_loader()
    {
        var game = MakeGame();
        File.WriteAllText(Path.Combine(game.ExeDirectory, "dinput8.dll"), "...Ultimate-ASI-Loader...");

        Assert.Equal("OptiScaler.asi", Installer.Install(game, _engine, _model, _testModel).ProxyName);
    }

    [Fact]
    public void Leaves_an_identical_model_that_was_already_there()
    {
        var game = MakeGame();
        File.Copy(_model, Path.Combine(game.ExeDirectory, "nvngx_dlssnr.dll"));

        var record = Installer.Install(game, _engine, _model, _testModel);
        Installer.Uninstall(game);

        Assert.DoesNotContain("nvngx_dlssnr.dll", record.Files.Keys);
        Assert.True(File.Exists(Path.Combine(game.ExeDirectory, "nvngx_dlssnr.dll")));
    }

    [Fact]
    public void Installs_the_engine_alone_without_a_model()
    {
        var game = MakeGame();
        var before = FilesIn(game);

        var record = Installer.Install(game, _engine, null, new InstallOptions { WithoutModel = true });

        Assert.DoesNotContain("nvngx_dlssnr.dll", record.Files.Keys);
        Assert.False(File.Exists(Path.Combine(game.ExeDirectory, "nvngx_dlssnr.dll")));
        Assert.True(File.Exists(Path.Combine(game.ExeDirectory, "winmm.dll")));

        Installer.Uninstall(game);
        Assert.Equal(before, FilesIn(game));
    }

    [Fact]
    public void Reinstalling_over_its_own_install_works()
    {
        var game = MakeGame();

        Installer.Install(game, _engine, _model, _testModel);
        var again = Installer.Install(game, _engine, _model, _testModel with { Style = NrStyle.Default });

        Assert.Equal("winmm.dll", again.ProxyName);
        Assert.Equal("0", IniEditor.Read(File.ReadAllText(Path.Combine(game.ExeDirectory, "OptiScaler.ini")), "DlssNr", "Style"));
    }

    [Fact]
    public void Refuses_a_game_with_anti_cheat()
    {
        var game = MakeGame("Shooter", "EasyAntiCheat");

        var e = Assert.Throws<InstallBlockedException>(() => Installer.Install(game, _engine, _model, _testModel));

        Assert.Contains("EasyAntiCheat", e.Message);
        Assert.Single(FilesIn(game));
    }

    [Fact]
    public void Refuses_another_optiscaler_install()
    {
        var game = MakeGame();
        File.WriteAllText(Path.Combine(game.ExeDirectory, "OptiScaler.ini"), "someone else's");

        Assert.Equal(InstallState.OtherOptiScaler, Installer.GetState(game));
        Assert.Throws<InstallBlockedException>(() => Installer.Install(game, _engine, _model, _testModel));
        Assert.Equal("someone else's", File.ReadAllText(Path.Combine(game.ExeDirectory, "OptiScaler.ini")));
    }

    [Fact]
    public void Refuses_a_model_that_is_not_signed_by_nvidia_unless_allowed()
    {
        var game = MakeGame();

        var e = Assert.Throws<InstallBlockedException>(() => Installer.Install(game, _engine, _model, new InstallOptions()));

        Assert.Contains("not signed by NVIDIA", e.Message);
        Assert.Single(FilesIn(game));
    }

    [Fact]
    public void Refuses_a_running_game()
    {
        // This test process is a running exe; pretend it is the game.
        var exe = Environment.ProcessPath!;
        var game = new Game("Running", Path.GetDirectoryName(exe)!, exe, null, []);

        var e = Assert.Throws<InstallBlockedException>(() => Installer.Install(game, _engine, _model, _testModel));

        Assert.Contains("Close Running first", e.Message);
    }

    [Fact]
    public void Reads_a_real_authenticode_signature()
    {
        var signed = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "coreclr.dll");

        var info = ModelInfo.Inspect(signed)!;

        Assert.NotNull(info.Signer);
        Assert.Equal("Microsoft Corporation", info.SignerOrganization);
        Assert.False(info.IsSignedByNvidia);
        Assert.Null(ModelInfo.Inspect(_model)!.Signer);
    }
}
