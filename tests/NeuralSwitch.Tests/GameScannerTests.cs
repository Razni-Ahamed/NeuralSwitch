using NeuralSwitch.Core.Games;

namespace NeuralSwitch.Tests;

public sealed class GameScannerTests : IDisposable
{
    private readonly TempFolder _library = new();

    public void Dispose() => _library.Dispose();

    [Fact]
    public void Finds_the_exe_beside_dlss_and_skips_installer_leftovers()
    {
        _library.File(@"Marvel's Spider-Man 2\Spider-Man2.exe", 183_000_000);
        _library.File(@"Marvel's Spider-Man 2\nvngx_dlss.dll");
        _library.File(@"Marvel's Spider-Man 2\_Redist\vc_redist.x64.exe", 24_000_000);
        _library.File(@"Marvel's Spider-Man 2\_Update to v1.131.0\Spider-Man2.exe", 190_000_000);

        var game = Assert.Single(GameScanner.Scan([_library.Path]));

        Assert.Equal("Marvel's Spider-Man 2", game.Name);
        Assert.Equal(Path.Combine(_library.Path, "Marvel's Spider-Man 2", "Spider-Man2.exe"), game.ExePath);
        Assert.False(game.HasAntiCheat);
    }

    [Fact]
    public void Finds_dlss_inside_a_streamline_folder()
    {
        _library.File(@"AC Black Flag Resynced\ACBlackFlag.exe", 457_000_000);
        _library.File(@"AC Black Flag Resynced\NVStreamline\production\nvngx_dlss.dll");
        _library.File(@"AC Black Flag Resynced\NVStreamline\production\sl.interposer.dll");

        var game = Assert.Single(GameScanner.Scan([_library.Path]));

        Assert.Equal("ACBlackFlag.exe", game.ExeName);
        Assert.Equal(Path.Combine(_library.Path, "AC Black Flag Resynced"), game.ExeDirectory);
    }

    [Fact]
    public void Picks_the_unreal_shipping_exe_over_the_bootstrap_and_crash_reporter()
    {
        _library.File(@"Some UE Game\SomeGame.exe", 300_000);
        _library.File(@"Some UE Game\SomeGame\Binaries\Win64\SomeGame-Win64-Shipping.exe", 120_000_000);
        _library.File(@"Some UE Game\Engine\Binaries\Win64\CrashReportClient.exe", 140_000_000);
        _library.File(@"Some UE Game\Engine\Plugins\Runtime\Nvidia\DLSS\Binaries\ThirdParty\Win64\nvngx_dlss.dll");

        var game = Assert.Single(GameScanner.Scan([_library.Path]));

        Assert.Equal("SomeGame-Win64-Shipping.exe", game.ExeName);
    }

    [Fact]
    public void Flags_anti_cheat()
    {
        _library.File(@"Online Shooter\Shooter.exe", 90_000_000);
        _library.File(@"Online Shooter\nvngx_dlss.dll");
        _library.File(@"Online Shooter\EasyAntiCheat\EasyAntiCheat_EOS_Setup.exe", 50_000_000);
        _library.File(@"Online Shooter\BEService_x64.exe", 1_000);

        var game = Assert.Single(GameScanner.Scan([_library.Path]));

        Assert.True(game.HasAntiCheat);
        Assert.Equal(["BattlEye", "EasyAntiCheat"], game.AntiCheat);
        Assert.Equal("Shooter.exe", game.ExeName);
    }

    [Fact]
    public void Ignores_games_without_dlss_and_missing_folders()
    {
        _library.File(@"Indie Game\Indie.exe", 10_000_000);

        Assert.Empty(GameScanner.Scan([_library.Path, Path.Combine(_library.Path, "does-not-exist")]));
    }
}
