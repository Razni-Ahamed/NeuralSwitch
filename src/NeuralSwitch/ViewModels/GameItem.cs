using System.Windows.Media;
using NeuralSwitch.Core.Games;
using NeuralSwitch.Core.Install;

namespace NeuralSwitch.ViewModels;

/// <summary>How a game's state pill is coloured.</summary>
public enum GameStateKind
{
    Neutral,
    Ready,
    Playing,
    Blocked,
}

/// <summary>One game in the library.</summary>
public sealed class GameItem(Game game, InstallState state) : ObservableObject
{
    private bool _isPlaying;
    private ImageSource? _icon;

    public Game Game { get; } = game;
    public InstallState State { get; } = state;

    public string Name => Game.Name;
    public string DlssText => $"DLSS {TrimVersion(Game.DlssVersion)}";
    public string ExeName => Game.ExeName;
    public string Folder => Game.ExeDirectory;

    /// <summary>Its icon, read in the background after the list appears. Null until then, or when the exe has none.</summary>
    public ImageSource? Icon
    {
        get => _icon;
        set => Set(ref _icon, value);
    }

    public bool IsPrepared => State == InstallState.Installed;
    public bool IsBlocked => !IsPlaying && (Game.HasAntiCheat || State == InstallState.OtherOptiScaler);

    // Not while it runs: its files are in use, and a running game with the engine connected has it already.
    public bool CanPrepare => State == InstallState.NotInstalled && !Game.HasAntiCheat && !IsPlaying;

    // One source for the words and the colour, so the pill never says one thing in the colour of another.
    public GameStateKind StateKind => IsPlaying ? GameStateKind.Playing
                                    : IsBlocked ? GameStateKind.Blocked
                                    : IsPrepared ? GameStateKind.Ready
                                    : GameStateKind.Neutral;

    public string StateText => StateKind switch
    {
        GameStateKind.Playing => "Playing",
        GameStateKind.Ready => "Ready",
        GameStateKind.Blocked => Game.HasAntiCheat ? "Anti-cheat" : "Other OptiScaler install",
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
            if (!Set(ref _isPlaying, value))
                return;

            foreach (var name in new[] { nameof(StateText), nameof(StateKind), nameof(CanPrepare), nameof(IsBlocked) })
                OnPropertyChanged(name);
        }
    }

    private static string TrimVersion(string? version) =>
        version is null ? "?" : version.EndsWith(".0", StringComparison.Ordinal) ? version[..^2] : version;
}

/// <summary>A running game with the engine loaded, for choosing which one to control when there are several.</summary>
public sealed class RunningGame(int pid, string title) : ObservableObject
{
    public int Pid { get; } = pid;
    public string Title { get; } = title;
}
