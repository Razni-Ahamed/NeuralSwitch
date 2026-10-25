using System.Runtime.InteropServices;
using NeuralSwitch.Core.Model;

namespace NeuralSwitch.Tests;

public sealed class ModelLocatorTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static ModelInfo Model(string path, string? organization) => new(path, "310.9.0.0", 1, organization is null ? null : "x", organization);

    [Fact]
    public void A_downloaded_bin_counts_only_when_nvidia_signed_it()
    {
        Assert.True(ModelLocator.Accept(Model(@"C:\ProgramData\NVIDIA\NGX\models\dlssnr\versions\1\files\160_ABC.bin", "NVIDIA Corporation")));
        Assert.False(ModelLocator.Accept(Model(@"C:\ProgramData\NVIDIA\NGX\models\dlssnr\versions\1\files\160_ABC.bin", null)));
        Assert.False(ModelLocator.Accept(Model(@"C:\ProgramData\NVIDIA\NGX\models\dlssnr\versions\1\files\160_ABC.bin", "Microsoft Corporation")));
    }

    [Fact]
    public void A_file_named_like_the_model_is_listed_even_unsigned_so_the_user_can_be_warned()
    {
        Assert.True(ModelLocator.Accept(Model(@"D:\Games\Some Game\nvngx_dlssnr.dll", null)));
    }

    [Fact]
    public void Finds_a_named_model_in_a_game_folder_and_skips_unsigned_downloads()
    {
        var ngx = _temp.Folder("ngx");
        _temp.File(@"ngx\dlssnr\versions\1\files\160_UNSIGNED.bin", text: "not NVIDIA's");
        File.Copy(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "coreclr.dll"),
                  Path.Combine(_temp.Folder(@"ngx\dlssnr\versions\2\files"), "160_MICROSOFT.bin"));
        var inGame = _temp.File(@"games\Some Game\nvngx_dlssnr.dll", text: "model");

        var found = ModelLocator.Find([Path.Combine(_temp.Path, "games")], ngx);

        var model = Assert.Single(found, m => m.Path.StartsWith(_temp.Path, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(inGame, model.Path);
        Assert.False(model.IsSignedByNvidia);
    }
}
