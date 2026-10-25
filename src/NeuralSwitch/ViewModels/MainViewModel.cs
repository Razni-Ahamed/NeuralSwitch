using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using NeuralSwitch.Core;
using NeuralSwitch.Core.Engine;
using NeuralSwitch.Core.Games;
using NeuralSwitch.Core.Install;
using NeuralSwitch.Core.Model;

namespace NeuralSwitch.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    // After the user moves the slider, the engine's answers do not move it back for this long.
    private static readonly TimeSpan SliderHold = TimeSpan.FromSeconds(1.5);

    private readonly AppSettings _settings;
    private readonly DispatcherTimer _poll;
    private readonly DispatcherTimer _intensityDebounce;

    private int? _pid;
    private int _missedPolls;
    private bool _polling;
    private EngineState? _state;
    private DateTime _sliderMovedUtc;
    private bool _updatingFromEngine;

    private string _message = "";
    private double _intensity = 1.0;
    private ModelInfo? _model;
    private EnginePackage? _engine;

    public MainViewModel(AppSettings settings)
    {
        _settings = settings;

        ToggleCommand = new AsyncCommand(() => Send(c => c.ToggleAsync()), () => IsConnected);
        StyleCommand = new AsyncCommand(p => Send(c => c.SetAsync(enabled: true, style: (NrStyle)p!)), _ => IsConnected);
        RetryCommand = new AsyncCommand(() => Send(c => c.RetryAsync()), () => IsConnected);
        PrepareCommand = new AsyncCommand(p => PrepareAsync((GameItem)p!), p => p is GameItem { CanPrepare: true });
        RemoveCommand = new AsyncCommand(p => RemoveAsync((GameItem)p!), p => p is GameItem { IsPrepared: true, IsPlaying: false });
        RescanCommand = new AsyncCommand(RescanAsync);
        AddFolderCommand = new AsyncCommand(AddFolderAsync);
        RemoveFolderCommand = new AsyncCommand(p => RemoveFolderAsync((string)p!));
        LocateModelCommand = new AsyncCommand(LocateModelAsync);

        _poll = new DispatcherTimer { Interval = PollInterval };
        _poll.Tick += async (_, _) => await PollAsync();

        _intensityDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _intensityDebounce.Tick += async (_, _) =>
        {
            _intensityDebounce.Stop();
            await Send(c => c.SetAsync(intensity: (float)Math.Round(_intensity, 2)));
        };
    }

    public ICommand ToggleCommand { get; }
    public ICommand StyleCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand PrepareCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand RemoveFolderCommand { get; }
    public ICommand LocateModelCommand { get; }

    public ObservableCollection<GameItem> Games { get; } = [];
    public ObservableCollection<string> LibraryFolders { get; } = [];
    public IReadOnlyList<NrStyle> Styles => NrStyles.All;

    public char ToggleKey => char.ToUpperInvariant(_settings.ToggleHotkey);
    public char StyleKey => char.ToUpperInvariant(_settings.StyleHotkey);
    public string ToggleHotkey { get; private set; } = "";
    public string StyleHotkey { get; private set; } = "";

    /// <summary>Called once the window has tried to register the hotkeys. One another program holds is
    /// shown as unavailable rather than interrupting with a dialog.</summary>
    public void ReportHotkeys(bool toggleRegistered, bool styleRegistered)
    {
        ToggleHotkey = toggleRegistered ? $"Ctrl+Alt+{ToggleKey}" : "(taken)";
        StyleHotkey = styleRegistered ? $"Ctrl+Alt+{StyleKey}" : "(taken)";
        OnPropertyChanged(nameof(ToggleHotkey));
        OnPropertyChanged(nameof(StyleHotkey));

        if (!toggleRegistered || !styleRegistered)
            Message = _hotkeyWarning =
                $"Another program already uses {(toggleRegistered ? "" : $"Ctrl+Alt+{ToggleKey} ")}{(styleRegistered ? "" : $"Ctrl+Alt+{StyleKey}")}. " +
                $"Pick other letters in {AppSettings.FilePath} (ToggleHotkey, StyleHotkey).";
    }

    private string _hotkeyWarning = "";

    // ---- Now playing -------------------------------------------------------------------------------

    public bool IsConnected => _pid is not null && _state is not null;
    public bool IsEnabled => _state?.Enabled == true;
    public NrStyle CurrentStyle => _state?.Style ?? _settings.DefaultStyle;
    public bool HasFailure => IsEnabled && _state?.Failure.Length > 0;

    public string GameTitle => _state is { } s ? Games.FirstOrDefault(g => g.Game.ExeName.Equals(s.Exe, StringComparison.OrdinalIgnoreCase))?.Name ?? s.Exe
                                               : "No game connected";

    public string StatusText => _state switch
    {
        null => "Prepare a game below, start it, and it appears here.",
        { Rendering: false } => "Paused, minimised or loading.",
        { Enabled: false } => "Neural Rendering is off.",
        { Failure.Length: > 0 } s => $"Could not start: {s.Failure}",
        { Compare: "off" } => "Switched off by the in-game comparison key (F6). Press it again in game.",
        { Running: true } s => $"Running · {s.Fps:0} fps rendered · {s.NrMs:0.0} ms per frame",
        _ => "Starting… The model loads the next time the game renders with DLSS. Make sure DLSS is on in the game's graphics settings.",
    };

    public string ToggleText => IsEnabled ? "ON" : "OFF";

    public double Intensity
    {
        get => _intensity;
        set
        {
            if (!Set(ref _intensity, Math.Clamp(value, 0, 2)))
                return;

            OnPropertyChanged(nameof(IntensityText));

            if (_updatingFromEngine)
                return;

            _sliderMovedUtc = DateTime.UtcNow;
            _intensityDebounce.Stop();
            _intensityDebounce.Start();
        }
    }

    public string IntensityText => _intensity.ToString("0.00");

    // ---- Setup -------------------------------------------------------------------------------------

    public string Message
    {
        get => _message;
        private set => Set(ref _message, value);
    }

    public bool ModelOk => _model?.IsSignedByNvidia == true;

    public string ModelText => _model switch
    {
        null => "NVIDIA's model (nvngx_dlssnr.dll) is not on this PC yet. It comes with NVIDIA drivers and games that ship DLSS 5.",
        { IsSignedByNvidia: true } m => $"NVIDIA model {m.Version} · signed by NVIDIA",
        var m => $"Model {m.Version} is NOT signed by NVIDIA. It may have been modified.",
    };

    public bool EngineOk => _engine is not null;

    public string EngineText => _engine is null
        ? "The engine was not found beside the app. Unzip the release again with its engine folder, or build it (engine\\build-engine.ps1)."
        : $"Engine ready · {_engine.Directory}";

    // ---- Startup ---------------------------------------------------------------------------------

    public async Task StartAsync()
    {
        foreach (var folder in _settings.LibraryFolders)
            LibraryFolders.Add(folder);

        _engine = EnginePackage.Find(_settings.EngineDirectory);
        OnPropertyChanged(nameof(EngineOk));
        OnPropertyChanged(nameof(EngineText));

        await RescanAsync();
        await PollAsync();
        _poll.Start();
    }

    public void Stop() => _poll.Stop();

    // ---- Live control ----------------------------------------------------------------------------

    public Task ToggleFromHotkey() => Send(c => c.ToggleAsync());

    public Task NextStyleFromHotkey() => Send(c => c.SetAsync(enabled: true, style: CurrentStyle.Next()));

    private async Task Send(Func<EngineClient, Task<EngineState>> action)
    {
        if (_pid is not { } pid)
        {
            Message = "No game is connected. Start a prepared game first.";
            return;
        }

        try
        {
            Apply(await action(new EngineClient(pid)));
            Message = _state?.Applied == false ? "The game is not rendering right now; the change applies on its next frame." : "";
        }
        catch (EngineException e)
        {
            Message = e.Message;
        }
    }

    private async Task PollAsync()
    {
        if (_polling)
            return;

        _polling = true;

        try
        {
            var running = EngineDiscovery.FindRunning();

            if (_pid is { } current && !running.Contains(current))
                Disconnect();

            _pid ??= running.Count > 0 ? running[0] : null;

            if (_pid is not { } pid)
                return;

            try
            {
                Apply(await new EngineClient(pid).GetAsync());
                _missedPolls = 0;
            }
            catch (EngineException)
            {
                // A game busy loading can miss a poll; three in a row means it is gone.
                if (++_missedPolls >= 3)
                    Disconnect();
            }
        }
        finally
        {
            _polling = false;
        }
    }

    private void Apply(EngineState state)
    {
        _state = state;

        if (DateTime.UtcNow - _sliderMovedUtc > SliderHold)
        {
            _updatingFromEngine = true;
            Intensity = state.Intensity;
            _updatingFromEngine = false;
        }

        foreach (var game in Games)
            game.IsPlaying = game.Game.ExeName.Equals(state.Exe, StringComparison.OrdinalIgnoreCase);

        NotifyLive();
    }

    private void Disconnect()
    {
        _pid = null;
        _state = null;
        _missedPolls = 0;

        foreach (var game in Games)
            game.IsPlaying = false;

        NotifyLive();
    }

    private void NotifyLive()
    {
        foreach (var name in new[]
                 {
                     nameof(IsConnected), nameof(IsEnabled), nameof(CurrentStyle), nameof(HasFailure), nameof(GameTitle),
                     nameof(StatusText), nameof(ToggleText),
                 })
            OnPropertyChanged(name);

        CommandManager.InvalidateRequerySuggested();
    }

    // ---- Games -----------------------------------------------------------------------------------

    private async Task RescanAsync()
    {
        Message = "Looking for games…";

        var folders = LibraryFolders.ToList();
        var (games, model) = await Task.Run(() =>
        {
            var found = GameScanner.Scan(folders);
            var items = found.Select(g => new GameItem(g, Installer.GetState(g))).ToList();
            var picked = _settings.ModelPath is { } path && ModelInfo.Inspect(path) is { } chosen
                ? chosen
                : ModelLocator.Find(found.Select(g => g.Root)).FirstOrDefault();
            return (items, picked);
        });

        Games.Clear();

        foreach (var game in games)
        {
            game.IsPlaying = _state is { } s && game.Game.ExeName.Equals(s.Exe, StringComparison.OrdinalIgnoreCase);
            Games.Add(game);
        }

        _model = model;
        OnPropertyChanged(nameof(ModelOk));
        OnPropertyChanged(nameof(ModelText));
        OnPropertyChanged(nameof(GameTitle));

        Message = games.Count == 0 ? "No DLSS games found in the library folders." : _hotkeyWarning;
    }

    private async Task PrepareAsync(GameItem item)
    {
        if (_engine is null)
        {
            Message = "The engine was not found beside the app. Unzip the release again with its engine folder, or build it (engine\\build-engine.ps1).";
            return;
        }

        if (_model is null)
        {
            Message = "NVIDIA's model nvngx_dlssnr.dll is needed first. Use \"Locate model\" once you have it.";
            return;
        }

        var options = new InstallOptions { Style = _settings.DefaultStyle };

        if (!_model.IsSignedByNvidia)
        {
            var answer = MessageBox.Show(
                $"{_model.Path}\n\nThis file is not signed by NVIDIA. Modified copies of nvngx_dlssnr.dll circulate online, and some carry malware. " +
                "Only continue if you know exactly where this copy came from.\n\nUse it anyway?",
                "NeuralSwitch", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            if (answer != MessageBoxResult.Yes)
                return;

            options = options with { AllowUnsignedModel = true };
        }

        var engine = _engine;
        var modelPath = _model.Path;

        try
        {
            var record = await Task.Run(() => Installer.Install(item.Game, engine, modelPath, options));
            Message = $"{item.Name} is ready (the engine loads as {record.ProxyName}). Start the game and turn DLSS on in its settings.";
        }
        catch (Exception e) when (e is InstallBlockedException or IOException or UnauthorizedAccessException)
        {
            Message = e is UnauthorizedAccessException
                ? $"Windows did not allow changes in {item.Game.ExeDirectory}. Run NeuralSwitch as administrator for this folder."
                : e.Message;
        }

        await RescanAsync();
    }

    private async Task RemoveAsync(GameItem item)
    {
        try
        {
            await Task.Run(() => Installer.Uninstall(item.Game));
            Message = $"Removed the engine from {item.Name}. Its folder is as it was.";
        }
        catch (Exception e) when (e is InstallBlockedException or IOException or UnauthorizedAccessException)
        {
            Message = e.Message;
        }

        await RescanAsync();
    }

    private async Task AddFolderAsync()
    {
        var dialog = new OpenFolderDialog { Title = "Choose a folder that holds your games" };

        if (dialog.ShowDialog() != true || LibraryFolders.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase))
            return;

        LibraryFolders.Add(dialog.FolderName);
        SaveFolders();
        await RescanAsync();
    }

    private async Task RemoveFolderAsync(string folder)
    {
        LibraryFolders.Remove(folder);
        SaveFolders();
        await RescanAsync();
    }

    private async Task LocateModelAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Find NVIDIA's nvngx_dlssnr.dll",
            Filter = "DLSS 5 model (nvngx_dlssnr.dll)|nvngx_dlssnr.dll",
        };

        if (dialog.ShowDialog() != true)
            return;

        _settings.ModelPath = dialog.FileName;
        _settings.Save();
        await RescanAsync();
    }

    private void SaveFolders()
    {
        _settings.LibraryFolders = [.. LibraryFolders];
        _settings.Save();
    }
}
