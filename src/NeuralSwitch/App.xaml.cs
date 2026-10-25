using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NeuralSwitch.Core;
using NeuralSwitch.Core.Engine;
using NeuralSwitch.ViewModels;

namespace NeuralSwitch;

public partial class App : Application
{
    private Mutex? _instance;
    private EventWaitHandle? _showSignal;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        var args = e.Args.ToList();
        var settingsPath = TakeOption(args, "--settings");
        var previewDir = TakeOption(args, "--render-preview");

        // --render-preview <folder>: draws every page, in each state, against a simulated game into PNGs, and
        // records any broken binding. For checking the UI without a game or a screen recorder.
        if (previewDir is not null)
        {
            RenderPreview(previewDir, settingsPath);
            return;
        }

        // One NeuralSwitch at a time (per settings file): a second start brings the first one's window up
        // instead of fighting it for the hotkeys.
        var key = settingsPath is null ? "default" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(settingsPath))))[..12];
        _instance = new Mutex(true, $@"Local\NeuralSwitch-{key}", out var first);

        if (!first)
        {
            try
            {
                using var signal = EventWaitHandle.OpenExisting($@"Local\NeuralSwitch-Show-{key}");
                signal.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }

            Shutdown();
            return;
        }

        var window = new MainWindow(new MainViewModel(AppSettings.Load(settingsPath), settingsPath));
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\NeuralSwitch-Show-{key}");
        new Thread(() =>
        {
            while (_showSignal.WaitOne())
                Dispatcher.Invoke(window.ShowFromTray);
        }) { IsBackground = true, Name = "Second start" }.Start();

        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }

    private static string? TakeOption(List<string> args, string name)
    {
        var i = args.IndexOf(name);

        if (i < 0 || i + 1 >= args.Count)
            return null;

        var value = args[i + 1];
        args.RemoveRange(i, 2);
        return value;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Keep running where possible, and leave a trace for a bug report.
        try
        {
            var log = Path.Combine(Path.GetDirectoryName(AppSettings.FilePath)!, "error.log");
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log, $"{DateTime.Now:O}\n{e.Exception}\n\n");
        }
        catch (IOException)
        {
        }

        MessageBox.Show($"Something went wrong:\n\n{e.Exception.Message}\n\nDetails are in %APPDATA%\\NeuralSwitch\\error.log.",
                        "NeuralSwitch", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    // ---- Preview -------------------------------------------------------------------------------------

    private sealed class BindingErrors : TraceListener
    {
        public List<string> Messages { get; } = [];
        private readonly StringBuilder _line = new();

        public override void Write(string? message) => _line.Append(message);

        public override void WriteLine(string? message)
        {
            _line.Append(message);
            Messages.Add(_line.ToString());
            _line.Clear();
        }
    }

    private async void RenderPreview(string dir, string? settingsPath)
    {
        Directory.CreateDirectory(dir);

        var errors = new BindingErrors();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(errors);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;

        // A throwaway copy of the settings, so the preview changes nothing the user has.
        var settings = AppSettings.Load(settingsPath);
        settings.AcceptedCaution = 0;
        var scratch = Path.Combine(dir, "preview-settings.json");
        settings.Save(scratch);

        var vm = new MainViewModel(AppSettings.Load(scratch), scratch);
        var window = new MainWindow(vm, withTray: false)
        {
            Left = -20000, Top = -20000, Width = 1100, Height = 820, ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };

        window.Show();
        await Settle(3000);
        Capture(window, dir, "01-caution.png");

        vm.CautionChecked = true;
        vm.AcceptCautionCommand.Execute(null);
        await Settle(800);
        Capture(window, dir, "02-now-playing-empty.png");

        await using var engine = new SimulatedEngine(exe: "Spider-Man2.exe") { Enabled = true, Style = NrStyle.Natural, Intensity = 0.8f }.Start();
        await Settle(2000);
        Capture(window, dir, "03-now-playing-running.png");

        engine.Failure = "nvngx_dlssnr.dll was not found next to the game";
        await Settle(1200);
        Capture(window, dir, "04-now-playing-failed.png");

        engine.Failure = "";
        engine.Enabled = false;
        await Settle(1200);
        Capture(window, dir, "05-now-playing-off.png");

        foreach (var (page, file) in new[] { (Page.Games, "06-games.png"), (Page.Settings, "07-settings.png"), (Page.About, "08-about.png") })
        {
            vm.CurrentPage = page;
            await Settle(700);
            Capture(window, dir, file);
        }

        vm.CurrentPage = Page.Games;
        _ = vm.AskAsync("This model isn't signed by NVIDIA",
                        "Modified copies of nvngx_dlssnr.dll circulate online, and some carry malware. Only continue if you know exactly where this copy came from.",
                        @"D:\Downloads\nvngx_dlssnr.dll", "Use it anyway", "Cancel");
        await Settle(600);
        Capture(window, dir, "09-question.png");
        vm.AnswerNoCommand.Execute(null);

        File.WriteAllLines(Path.Combine(dir, "binding-errors.txt"), errors.Messages);
        window.Close();
        Shutdown(errors.Messages.Count == 0 ? 0 : 3);
    }

    private async Task Settle(int milliseconds)
    {
        await Task.Delay(milliseconds);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private void Capture(Window window, string dir, string file)
    {
        var root = (FrameworkElement)window.Content;
        var dpi = VisualTreeHelper.GetDpi(root);
        var bitmap = new RenderTargetBitmap((int)(root.ActualWidth * dpi.DpiScaleX), (int)(root.ActualHeight * dpi.DpiScaleY),
                                            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(dir, file));
        encoder.Save(stream);
    }
}
