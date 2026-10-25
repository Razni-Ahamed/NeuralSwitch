using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using NeuralSwitch.Interop;
using NeuralSwitch.ViewModels;

namespace NeuralSwitch;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly bool _withTray;
    private GlobalHotkeys? _hotkeys;
    private TrayIcon? _tray;
    private bool _quitting;

    /// <param name="withTray">False for previews and tests: no tray icon, and closing really closes.</param>
    public MainWindow(MainViewModel vm, bool withTray = true)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        _withTray = withTray;
        Loaded += async (_, _) => await _vm.StartAsync();
        _vm.HotkeysChanged += RegisterHotkeys;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        Dwm.UseDarkTitleBar(hwnd, 0x12, 0x15, 0x1B);

        _hotkeys = new GlobalHotkeys(hwnd);
        RegisterHotkeys();

        if (_withTray)
        {
            _tray = new TrayIcon(
                open: ShowFromTray,
                toggle: () => _ = _vm.ToggleFromHotkey(),
                style: s => _ = _vm.SetStyleFromTray(s),
                quit: Quit,
                state: () => (_vm.IsConnected, _vm.IsEnabled, _vm.CurrentStyle, _vm.GameTitle));
        }
    }

    private void RegisterHotkeys()
    {
        if (_hotkeys is null)
            return;

        _hotkeys.UnregisterAll();
        var toggle = _hotkeys.RegisterCtrlAlt(_vm.ToggleKey, () => _ = _vm.ToggleFromHotkey());
        var style = _hotkeys.RegisterCtrlAlt(_vm.StyleKey, () => _ = _vm.NextStyleFromHotkey());
        _vm.ReportHotkeys(toggle, style);
    }

    /// <summary>Brings the window back: from the tray, or when NeuralSwitch is started a second time.</summary>
    public void ShowFromTray()
    {
        Show();

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Closing keeps NeuralSwitch in the tray, so its hotkeys keep working in game.
        if (!_quitting && _tray is not null && _vm.CloseToTray)
        {
            e.Cancel = true;
            Hide();

            if (!_vm.TrayHintShown)
            {
                _tray.ShowHint("NeuralSwitch is still running", "Its hotkeys keep working. Use the icon here to open it or quit.");
                _vm.TrayHintShown = true;
            }

            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.Stop();
        _hotkeys?.Dispose();
        _tray?.Dispose();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    private void Quit()
    {
        _quitting = true;
        Close();
    }

    private void Quit_Click(object sender, RoutedEventArgs e) => Quit();
}
