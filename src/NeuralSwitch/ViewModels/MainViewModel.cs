using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using NeuralSwitch.Core;
using NeuralSwitch.Core.Engine;
using NeuralSwitch.Core.Games;
using NeuralSwitch.Core.Install;
using NeuralSwitch.Core.Model;
using NeuralSwitch.Interop;

namespace NeuralSwitch.ViewModels;

public enum Page
{
    NowPlaying,
    Games,
    Settings,
    About,
}

/// <summary>What the connected game's Neural Rendering is doing, as the status pill shows it.</summary>
public enum LiveStatus
{
    NotConnected,
    Paused,
    Off,
    Starting,
    Running,
    Failed,
    ComparisonOff,
}

public enum MessageKind
{
    Info,
    Success,
    Warning,
    Error,
}

public sealed class MainViewModel : ObservableObject
{
    /// <summary>Raised to the version of USER_CAUTION.md whenever its substance changes, so users accept it again.</summary>
    public const int CautionVersion = 1;

    private const string RepoUrl = "https://github.com/Razni-Ahamed/NeuralSwitch";

    private static readonly IReadOnlyDictionary<string, string> Links = new Dictionary<string, string>
    {
        ["repo"] = RepoUrl,
        ["guide"] = RepoUrl + "/blob/main/docs/USER_GUIDE.md",
        ["caution"] = RepoUrl + "/blob/main/USER_CAUTION.md",
        ["license"] = RepoUrl + "/blob/main/LICENSE",
        ["notices"] = RepoUrl + "/blob/main/THIRD_PARTY_NOTICES.md",
        ["issues"] = RepoUrl + "/issues",
        ["releases"] = RepoUrl + "/releases",
    };

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    // After the user moves the slider, the engine's answers do not move it back for this long.
    private static readonly TimeSpan SliderHold = TimeSpan.FromSeconds(1.5);

    private readonly AppSettings _settings;
    private readonly string? _settingsPath;
    private readonly DispatcherTimer _poll;
    private readonly DispatcherTimer _intensityDebounce;
    private readonly DispatcherTimer _messageTimer;

    private int? _pid;
    private int _missedPolls;
    private bool _polling;
    private EngineState? _state;
    private DateTime _sliderMovedUtc;
    private bool _updatingFromEngine;

    private Page _page = Page.NowPlaying;
    private bool _cautionChecked;
    private string _message = "";
    private MessageKind _messageKind;
    private double _intensity = 1.0;
    private ModelInfo? _model;
    private EnginePackage? _engine;
    private string _toggleKeyText;
    private string _styleKeyText;
    private bool _toggleRegistered = true;
    private bool _styleRegistered = true;

    public MainViewModel(AppSettings settings, string? settingsPath = null)
    {
        _settings = settings;
        _settingsPath = settingsPath;
        _toggleKeyText = settings.ToggleHotkey.ToString().ToUpperInvariant();
        _styleKeyText = settings.StyleHotkey.ToString().ToUpperInvariant();

        NavigateCommand = new AsyncCommand(p => { CurrentPage = Enum.Parse<Page>(p!.ToString()!); return Task.CompletedTask; });
        AcceptCautionCommand = new AsyncCommand(AcceptCaution, () => CautionChecked);

        ToggleCommand = new AsyncCommand(() => Send(c => c.ToggleAsync()), () => IsConnected);
        StyleCommand = new AsyncCommand(p => Send(c => c.SetAsync(enabled: true, style: (NrStyle)p!)), _ => IsConnected);
        RetryCommand = new AsyncCommand(() => Send(c => c.RetryAsync()), () => IsConnected);
        ResetIntensityCommand = new AsyncCommand(() => { Intensity = 1.0; return Task.CompletedTask; }, () => IsConnected);
        SelectGameCommand = new AsyncCommand(p => SelectGame((RunningGame)p!));

        PrepareCommand = new AsyncCommand(p => PrepareAsync((GameItem)p!), p => p is GameItem { CanPrepare: true });
        RemoveCommand = new AsyncCommand(p => RemoveAsync((GameItem)p!), p => p is GameItem { IsPrepared: true, IsPlaying: false });
        OpenGameFolderCommand = new AsyncCommand(p => OpenFolder(((GameItem)p!).Folder));
        RescanCommand = new AsyncCommand(RescanAsync);
        AddFolderCommand = new AsyncCommand(AddFolderAsync);
        RemoveFolderCommand = new AsyncCommand(p => RemoveFolderAsync((string)p!));

        LocateModelCommand = new AsyncCommand(LocateModelAsync);
        UseAutomaticModelCommand = new AsyncCommand(UseAutomaticModelAsync, () => _settings.ModelPath is not null);
        DefaultStyleCommand = new AsyncCommand(p => { DefaultStyle = (NrStyle)p!; return Task.CompletedTask; });
        ApplyHotkeysCommand = new AsyncCommand(ApplyHotkeys);
        OpenEngineFolderCommand = new AsyncCommand(() => OpenFolder(_engine?.Directory), () => _engine is not null);
        OpenSettingsFolderCommand = new AsyncCommand(() => OpenFolder(Path.GetDirectoryName(SettingsFile)));
        OpenLinkCommand = new AsyncCommand(p => OpenLink((string)p!));
        DismissMessageCommand = new AsyncCommand(() => { Message = ""; return Task.CompletedTask; });

        _poll = new DispatcherTimer { Interval = PollInterval };
        _poll.Tick += async (_, _) => await PollAsync();

        _intensityDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _intensityDebounce.Tick += async (_, _) =>
        {
            _intensityDebounce.Stop();
            await Send(c => c.SetAsync(intensity: (float)Math.Round(_intensity, 2)));
        };

        _messageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        _messageTimer.Tick += (_, _) =>
        {
            _messageTimer.Stop();
            Message = "";
        };
    }

    /// <summary>Raised when the hotkey letters change, so the window registers them again.</summary>
    public event Action? HotkeysChanged;

    // ---- Commands ------------------------------------------------------------------------------------

    public ICommand NavigateCommand { get; }
    public ICommand AcceptCautionCommand { get; }
    public ICommand ToggleCommand { get; }
    public ICommand StyleCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand ResetIntensityCommand { get; }
    public ICommand SelectGameCommand { get; }
    public ICommand PrepareCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand OpenGameFolderCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand RemoveFolderCommand { get; }
    public ICommand LocateModelCommand { get; }
    public ICommand UseAutomaticModelCommand { get; }
    public ICommand DefaultStyleCommand { get; }
    public ICommand ApplyHotkeysCommand { get; }
    public ICommand OpenEngineFolderCommand { get; }
    public ICommand OpenSettingsFolderCommand { get; }
    public ICommand OpenLinkCommand { get; }
    public ICommand DismissMessageCommand { get; }

    public ObservableCollection<GameItem> Games { get; } = [];
    public ObservableCollection<string> LibraryFolders { get; } = [];
    public ObservableCollection<RunningGame> RunningGames { get; } = [];
    public IReadOnlyList<NrStyle> Styles => NrStyles.All;

    // ---- Navigation and first run --------------------------------------------------------------------

    public Page CurrentPage
    {
        get => _page;
        set => Set(ref _page, value);
    }

    public bool NeedsCaution => _settings.AcceptedCaution < CautionVersion;

    public bool CautionChecked
    {
        get => _cautionChecked;
        set
        {
            if (Set(ref _cautionChecked, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    private Task AcceptCaution()
    {
        _settings.AcceptedCaution = CautionVersion;
        Save();
        OnPropertyChanged(nameof(NeedsCaution));
        return Task.CompletedTask;
    }

    // ---- Now playing ---------------------------------------------------------------------------------

    public bool IsConnected => _pid is not null && _state is not null;
    public bool IsEnabled => _state?.Enabled == true;
    public NrStyle CurrentStyle => _state?.Style ?? _settings.DefaultStyle;
    public bool HasSeveralRunning => RunningGames.Count > 1;
    public int? ConnectedPid => IsConnected ? _pid : null;

    private GameItem? ConnectedGame => _state is { } s
        ? Games.FirstOrDefault(g => g.ExeName.Equals(s.Exe, StringComparison.OrdinalIgnoreCase))
        : null;

    public string GameTitle => _state is null ? "No game connected" : ConnectedGame?.Name ?? Path.GetFileNameWithoutExtension(_state.Exe);
    public string GameSubtitle => _state is { } s ? $"{s.Exe} · process {s.Pid}" : "Waiting for a prepared game to start";
    public ImageSource? GameIcon => ConnectedGame?.Icon;

    public LiveStatus Status => _state switch
    {
        null => LiveStatus.NotConnected,
        { Rendering: false } => LiveStatus.Paused,
        { Enabled: false } => LiveStatus.Off,
        { Failure.Length: > 0 } => LiveStatus.Failed,
        { Compare: "off" } => LiveStatus.ComparisonOff,
        { Running: true } => LiveStatus.Running,
        _ => LiveStatus.Starting,
    };

    public string StatusTitle => Status switch
    {
        LiveStatus.NotConnected => "Not connected",
        LiveStatus.Paused => "Paused",
        LiveStatus.Off => "Off",
        LiveStatus.Starting => "Waiting for DLSS",
        LiveStatus.Running => "Running",
        LiveStatus.Failed => "Couldn't start",
        LiveStatus.ComparisonOff => "Paused by F6",
        _ => "",
    };

    public string StatusText => Status switch
    {
        LiveStatus.NotConnected => "Prepare a game on the Games page, start it with DLSS on, and it appears here.",
        LiveStatus.Paused => "The game is paused, minimised or loading. Changes apply as soon as it draws again.",
        LiveStatus.Off => $"Neural Rendering is off. Turn it on with the switch, or press {ToggleHotkey} in the game.",
        LiveStatus.Starting => "It's on, and starts the next time the game renders with DLSS. Make sure DLSS is turned on in the game's graphics settings.",
        LiveStatus.Running => "Neural Rendering is working on every frame the game renders.",
        LiveStatus.Failed => _state!.Failure,
        LiveStatus.ComparisonOff => "The in-game comparison key (F6) switched it off. Press F6 in the game, or turn it on here.",
        _ => "",
    };

    public string PowerText => IsEnabled ? "On" : "Off";
    public string FpsText => _state is { Rendering: true, Fps: > 0 } s ? s.Fps.ToString("0") : "—";
    public string CostText => _state is { Running: true, NrMs: > 0 } s ? s.NrMs.ToString("0.0") : "—";
    public string StyleText => CurrentStyle.ToString();

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

    // ---- Setup state ---------------------------------------------------------------------------------

    public bool ModelOk => _model?.IsSignedByNvidia == true;

    public string ModelTitle => _model switch
    {
        null => "NVIDIA's model isn't on this PC yet",
        { IsSignedByNvidia: true } m => $"NVIDIA model {m.Version}",
        var m => $"Model {m.Version} is not signed by NVIDIA",
    };

    public string ModelDetail => _model switch
    {
        null => "nvngx_dlssnr.dll comes with NVIDIA drivers and games that ship DLSS 5. NeuralSwitch never downloads it.",
        { IsSignedByNvidia: true } m => $"Signed by NVIDIA · {m.Path}",
        var m => $"It may have been modified. {m.Path}",
    };

    public bool ModelIsManual => _settings.ModelPath is not null;

    public bool EngineOk => _engine is not null;
    public string EngineTitle => _engine is null ? "Engine not found" : "Engine ready";

    public string EngineDetail => _engine is null
        ? "The engine folder is missing from beside NeuralSwitch.exe. Unzip the release again, keeping its folders."
        : $"OptiScaler-DLSS5 {ShortCommit(_engine.Commit)} with the NeuralSwitch patch · {_engine.Directory}";

    public bool CanPrepareAny => ModelOk && EngineOk;

    public string? SetupWarning => !EngineOk ? EngineDetail
                                 : _model is null ? "Preparing a game needs NVIDIA's model, nvngx_dlssnr.dll, which isn't on this PC yet. It comes with NVIDIA drivers and games that ship DLSS 5."
                                 : !ModelOk ? "The model file found is not signed by NVIDIA. NeuralSwitch will ask before using it."
                                 : null;

    public string GameCountText => Games.Count switch
    {
        0 => "No DLSS games found",
        1 => "1 DLSS game",
        var n => $"{n} DLSS games",
    };

    // ---- Settings ------------------------------------------------------------------------------------

    public string SettingsFile => _settingsPath ?? AppSettings.FilePath;

    public NrStyle DefaultStyle
    {
        get => _settings.DefaultStyle;
        set
        {
            if (_settings.DefaultStyle == value)
                return;

            _settings.DefaultStyle = value;
            Save();
            OnPropertyChanged();
        }
    }

    public bool CloseToTray
    {
        get => _settings.CloseToTray;
        set
        {
            if (_settings.CloseToTray == value)
                return;

            _settings.CloseToTray = value;
            Save();
            OnPropertyChanged();
        }
    }

    public string ToggleKeyText
    {
        get => _toggleKeyText;
        set => Set(ref _toggleKeyText, (value ?? "").Trim().ToUpperInvariant());
    }

    public string StyleKeyText
    {
        get => _styleKeyText;
        set => Set(ref _styleKeyText, (value ?? "").Trim().ToUpperInvariant());
    }

    public char ToggleKey => char.ToUpperInvariant(_settings.ToggleHotkey);
    public char StyleKey => char.ToUpperInvariant(_settings.StyleHotkey);
    public string ToggleHotkey => $"Ctrl+Alt+{ToggleKey}";
    public string StyleHotkey => $"Ctrl+Alt+{StyleKey}";
    public bool HotkeysOk => _toggleRegistered && _styleRegistered;

    public string HotkeyStatus => HotkeysOk
        ? "Both hotkeys are active, and work while a game has focus."
        : $"Another program already uses {string.Join(" and ", TakenHotkeys())}. Choose another letter.";

    public bool TrayHintShown
    {
        get => _settings.TrayHintShown;
        set
        {
            _settings.TrayHintShown = value;
            Save();
        }
    }

    public string VersionText => $"Version {Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?"}";

    // ---- Messages ------------------------------------------------------------------------------------

    public string Message
    {
        get => _message;
        private set
        {
            if (Set(ref _message, value))
                OnPropertyChanged(nameof(HasMessage));
        }
    }

    public MessageKind MessageKind
    {
        get => _messageKind;
        private set => Set(ref _messageKind, value);
    }

    public bool HasMessage => Message.Length > 0;

    private void Show(string text, MessageKind kind = MessageKind.Info)
    {
        MessageKind = kind;
        Message = text;
        _messageTimer.Stop();

        // Problems stay until dismissed or replaced; news goes away by itself.
        if (kind is MessageKind.Info or MessageKind.Success)
            _messageTimer.Start();
    }

    // ---- Questions -----------------------------------------------------------------------------------

    private TaskCompletionSource<bool>? _question;

    /// <summary>Asks in the app's own dialog (styled, and readable by screen readers, unlike a Windows message box).
    /// True for the first answer.</summary>
    internal Task<bool> AskAsync(string title, string text, string detail, string yes, string no)
    {
        QuestionTitle = title;
        QuestionText = text;
        QuestionDetail = detail;
        QuestionYes = yes;
        QuestionNo = no;
        _question = new TaskCompletionSource<bool>();

        foreach (var name in new[] { nameof(QuestionTitle), nameof(QuestionText), nameof(QuestionDetail), nameof(QuestionYes), nameof(QuestionNo), nameof(IsAsking) })
            OnPropertyChanged(name);

        return _question.Task;
    }

    private Task Answer(bool yes)
    {
        var question = _question;
        _question = null;
        OnPropertyChanged(nameof(IsAsking));
        question?.TrySetResult(yes);
        return Task.CompletedTask;
    }

    public bool IsAsking => _question is not null;
    public string QuestionTitle { get; private set; } = "";
    public string QuestionText { get; private set; } = "";
    public string QuestionDetail { get; private set; } = "";
    public string QuestionYes { get; private set; } = "";
    public string QuestionNo { get; private set; } = "";
    public ICommand AnswerYesCommand => _answerYes ??= new AsyncCommand(() => Answer(true));
    public ICommand AnswerNoCommand => _answerNo ??= new AsyncCommand(() => Answer(false));
    private ICommand? _answerYes;
    private ICommand? _answerNo;

    // ---- Lifetime ------------------------------------------------------------------------------------

    public async Task StartAsync()
    {
        foreach (var folder in _settings.LibraryFolders)
            LibraryFolders.Add(folder);

        _engine = EnginePackage.Find(_settings.EngineDirectory);
        NotifySetup();

        await RescanAsync();
        await PollAsync();
        _poll.Start();
    }

    public void Stop() => _poll.Stop();

    /// <summary>Called once the window has tried to register the hotkeys.</summary>
    public void ReportHotkeys(bool toggleRegistered, bool styleRegistered)
    {
        _toggleRegistered = toggleRegistered;
        _styleRegistered = styleRegistered;

        foreach (var name in new[] { nameof(ToggleHotkey), nameof(StyleHotkey), nameof(HotkeysOk), nameof(HotkeyStatus), nameof(StatusText) })
            OnPropertyChanged(name);

        if (!HotkeysOk)
            Show($"Another program already uses {string.Join(" and ", TakenHotkeys())}. Pick another letter in Settings.", MessageKind.Warning);
    }

    private IEnumerable<string> TakenHotkeys()
    {
        if (!_toggleRegistered)
            yield return ToggleHotkey;

        if (!_styleRegistered)
            yield return StyleHotkey;
    }

    // ---- Live control --------------------------------------------------------------------------------

    public Task ToggleFromHotkey() => Send(c => c.ToggleAsync());

    public Task NextStyleFromHotkey() => Send(c => c.SetAsync(enabled: true, style: CurrentStyle.Next()));

    public Task SetStyleFromTray(NrStyle style) => Send(c => c.SetAsync(enabled: true, style: style));

    private async Task Send(Func<EngineClient, Task<EngineState>> action)
    {
        if (_pid is not { } pid)
        {
            Show("No game is connected. Start a prepared game first.", MessageKind.Warning);
            return;
        }

        try
        {
            Apply(await action(new EngineClient(pid)));

            if (_state?.Applied == false)
                Show("The game isn't rendering right now; the change applies on its next frame.");
        }
        catch (EngineException e)
        {
            Show(e.Message, MessageKind.Error);
        }
    }

    private Task SelectGame(RunningGame game)
    {
        _pid = game.Pid;
        _state = null;
        NotifyLive();
        return PollAsync();
    }

    private async Task PollAsync()
    {
        if (_polling)
            return;

        _polling = true;

        try
        {
            var running = EngineDiscovery.FindRunning();
            SyncRunningGames(running);

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

    private void SyncRunningGames(IReadOnlyList<int> running)
    {
        if (RunningGames.Select(g => g.Pid).SequenceEqual(running))
            return;

        RunningGames.Clear();

        foreach (var pid in running)
            RunningGames.Add(new RunningGame(pid, ProcessTitle(pid)));

        OnPropertyChanged(nameof(HasSeveralRunning));
    }

    private string ProcessTitle(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            var exe = process.ProcessName + ".exe";
            return Games.FirstOrDefault(g => g.ExeName.Equals(exe, StringComparison.OrdinalIgnoreCase))?.Name ?? process.ProcessName;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return $"Process {pid}";
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
            game.IsPlaying = game.ExeName.Equals(state.Exe, StringComparison.OrdinalIgnoreCase);

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
                     nameof(IsConnected), nameof(IsEnabled), nameof(CurrentStyle), nameof(GameTitle), nameof(GameSubtitle),
                     nameof(GameIcon), nameof(Status), nameof(StatusTitle), nameof(StatusText), nameof(PowerText), nameof(FpsText),
                     nameof(CostText), nameof(StyleText), nameof(ConnectedPid),
                 })
            OnPropertyChanged(name);

        CommandManager.InvalidateRequerySuggested();
    }

    // ---- Games ---------------------------------------------------------------------------------------

    private async Task RescanAsync()
    {
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
            game.IsPlaying = _state is { } s && game.ExeName.Equals(s.Exe, StringComparison.OrdinalIgnoreCase);
            Games.Add(game);
        }

        _model = model;
        NotifySetup();
        OnPropertyChanged(nameof(GameCountText));
        NotifyLive();

        // Icons after the list is up: reading them is quick, but not free for a large library.
        foreach (var game in games)
        {
            var path = game.Game.ExePath;
            game.Icon = await Task.Run(() => IconExtractor.FromExe(path, 64));

            if (game.IsPlaying)
                OnPropertyChanged(nameof(GameIcon));
        }
    }

    private async Task PrepareAsync(GameItem item)
    {
        if (_engine is null)
        {
            Show(EngineDetail, MessageKind.Error);
            return;
        }

        if (_model is null)
        {
            Show("Preparing needs NVIDIA's model, nvngx_dlssnr.dll. Use Locate model in Settings once you have it.", MessageKind.Warning);
            return;
        }

        var options = new InstallOptions { Style = _settings.DefaultStyle };

        if (!_model.IsSignedByNvidia)
        {
            var useIt = await AskAsync(
                "This model isn't signed by NVIDIA",
                "Modified copies of nvngx_dlssnr.dll circulate online, and some carry malware. Only continue if you know exactly where this copy came from.",
                _model.Path, "Use it anyway", "Cancel");

            if (!useIt)
                return;

            options = options with { AllowUnsignedModel = true };
        }

        var engine = _engine;
        var modelPath = _model.Path;

        try
        {
            var record = await Task.Run(() => Installer.Install(item.Game, engine, modelPath, options));
            Show($"{item.Name} is ready (the engine loads as {record.ProxyName}). Start the game and turn DLSS on in its settings.", MessageKind.Success);
        }
        catch (Exception e) when (e is InstallBlockedException or IOException or UnauthorizedAccessException)
        {
            Show(e is UnauthorizedAccessException
                     ? $"Windows did not allow changes in {item.Folder}. Run NeuralSwitch as administrator for this folder."
                     : e.Message,
                 MessageKind.Error);
        }

        await RescanAsync();
    }

    private async Task RemoveAsync(GameItem item)
    {
        try
        {
            await Task.Run(() => Installer.Uninstall(item.Game));
            Show($"Removed the engine from {item.Name}. Its folder is as it was.", MessageKind.Success);
        }
        catch (Exception e) when (e is InstallBlockedException or IOException or UnauthorizedAccessException)
        {
            Show(e.Message, MessageKind.Error);
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

    // ---- Settings ------------------------------------------------------------------------------------

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
        Save();
        await RescanAsync();
    }

    private async Task UseAutomaticModelAsync()
    {
        _settings.ModelPath = null;
        Save();
        await RescanAsync();
    }

    private Task ApplyHotkeys()
    {
        static bool Valid(string s) => s.Length == 1 && char.IsAsciiLetterOrDigit(s[0]);

        if (!Valid(ToggleKeyText) || !Valid(StyleKeyText))
        {
            Show("Each hotkey takes one letter or digit.", MessageKind.Warning);
            return Task.CompletedTask;
        }

        if (ToggleKeyText == StyleKeyText)
        {
            Show("The two hotkeys need different letters.", MessageKind.Warning);
            return Task.CompletedTask;
        }

        _settings.ToggleHotkey = ToggleKeyText[0];
        _settings.StyleHotkey = StyleKeyText[0];
        Save();
        HotkeysChanged?.Invoke();

        if (HotkeysOk)
            Show($"Hotkeys set: {ToggleHotkey} on/off, {StyleHotkey} next style.", MessageKind.Success);

        return Task.CompletedTask;
    }

    private Task OpenFolder(string? folder)
    {
        if (folder is not null && Directory.Exists(folder))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });

        return Task.CompletedTask;
    }

    private Task OpenLink(string key)
    {
        // Only the project's own pages, never an address from anywhere else.
        if (Links.TryGetValue(key, out var url))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        return Task.CompletedTask;
    }

    private void SaveFolders()
    {
        _settings.LibraryFolders = [.. LibraryFolders];
        Save();
    }

    private void Save()
    {
        try
        {
            _settings.Save(_settingsPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Show($"Couldn't save settings: {e.Message}", MessageKind.Error);
        }
    }

    private void NotifySetup()
    {
        foreach (var name in new[]
                 {
                     nameof(ModelOk), nameof(ModelTitle), nameof(ModelDetail), nameof(ModelIsManual), nameof(EngineOk),
                     nameof(EngineTitle), nameof(EngineDetail), nameof(CanPrepareAny), nameof(SetupWarning),
                 })
            OnPropertyChanged(name);

        CommandManager.InvalidateRequerySuggested();
    }

    private static string ShortCommit(string? commit) => commit is { Length: >= 7 } ? commit[..7] : commit ?? "";
}
