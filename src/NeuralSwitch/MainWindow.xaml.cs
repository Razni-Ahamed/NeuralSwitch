using System.Windows;
using System.Windows.Interop;
using NeuralSwitch.Interop;
using NeuralSwitch.ViewModels;

namespace NeuralSwitch;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private GlobalHotkeys? _hotkeys;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        Loaded += async (_, _) => await _vm.StartAsync();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hotkeys = new GlobalHotkeys(new WindowInteropHelper(this).Handle);
        var toggle = _hotkeys.RegisterCtrlAlt(_vm.ToggleKey, () => _ = _vm.ToggleFromHotkey());
        var style = _hotkeys.RegisterCtrlAlt(_vm.StyleKey, () => _ = _vm.NextStyleFromHotkey());
        _vm.ReportHotkeys(toggle, style);
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.Stop();
        _hotkeys?.Dispose();
        base.OnClosed(e);
    }
}
