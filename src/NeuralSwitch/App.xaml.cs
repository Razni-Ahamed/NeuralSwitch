using System.IO;
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
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // --render-preview <file.png>: draw the window, connected to a simulated game, into a picture and
        // exit. For checking the layout (and for the README) without a game or a screen recorder.
        if (e.Args is ["--render-preview", var png])
        {
            RenderPreview(png);
            return;
        }

        new MainWindow(new MainViewModel(AppSettings.Load())).Show();
    }

    private async void RenderPreview(string png)
    {
        var engine = new SimulatedEngine(exe: "Spider-Man2.exe") { Enabled = true, Style = NrStyle.Natural, Intensity = 0.8f }.Start();
        var window = new MainWindow(new MainViewModel(AppSettings.Load()))
        {
            Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual,
        };

        window.Show();

        // Long enough for the first scan and a couple of polls.
        await Task.Delay(TimeSpan.FromSeconds(3));
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

        var root = (FrameworkElement)window.Content;
        var dpi = VisualTreeHelper.GetDpi(root);
        var bitmap = new RenderTargetBitmap((int)(window.ActualWidth * dpi.DpiScaleX), (int)(window.ActualHeight * dpi.DpiScaleY),
                                            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);

        var background = new DrawingVisual();
        using (var dc = background.RenderOpen())
            dc.DrawRectangle((Brush)FindResource("Bg"), null, new Rect(0, 0, window.ActualWidth, window.ActualHeight));

        bitmap.Render(background);
        bitmap.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        await using (var file = File.Create(png))
            encoder.Save(file);

        window.Close();
        await engine.DisposeAsync();
        Shutdown();
    }
}
