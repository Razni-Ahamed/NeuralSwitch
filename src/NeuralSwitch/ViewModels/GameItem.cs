using NeuralSwitch.Core.Games;
using NeuralSwitch.Core.Install;

namespace NeuralSwitch.ViewModels;

/// <summary>One row of the game list.</summary>
public sealed class GameItem(Game game, InstallState state) : ObservableObject
{
    private bool _isPlaying;

    public Game Game { get; } = game;
    public InstallState State { get; } = state;

    public string Name => Game.Name;
    public string Detail => $"DLSS {TrimVersion(Game.DlssVersion)} · {Game.ExeName}";

    public bool IsPrepared => State == InstallState.Installed;
    public bool IsBlocked => Game.HasAntiCheat || State == InstallState.OtherOptiScaler;
    public bool CanPrepare => State == InstallState.NotInstalled && !Game.HasAntiCheat;

    public string StateText => State switch
    {
        InstallState.Installed => IsPlaying ? "Playing" : "Ready",
        InstallState.OtherOptiScaler => "Other OptiScaler install",
        _ when Game.HasAntiCheat => "Anti-cheat",
        _ => "Not prepared",
    };

    public string? BlockedReason => Game.HasAntiCheat
        ? $"Uses {string.Join(" and ", Game.AntiCheat)}. Loading the engine could get your account banned, so it stays off."
        : State == InstallState.OtherOptiScaler
            ? "OptiScaler is already here from another installer. Remove that first."
            : null;

    /// <summary>The game is running with the engine connected.</summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (Set(ref _isPlaying, value))
                OnPropertyChanged(nameof(StateText));
        }
    }

    private static string TrimVersion(string? version) =>
        version is null ? "?" : version.EndsWith(".0", StringComparison.Ordinal) ? version[..^2] : version;
}
