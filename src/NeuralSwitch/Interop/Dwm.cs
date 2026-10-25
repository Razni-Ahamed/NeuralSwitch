using System.Runtime.InteropServices;

namespace NeuralSwitch.Interop;

/// <summary>Asks Windows to draw the title bar dark, to match the window, instead of the light default.</summary>
internal static class Dwm
{
    private const int UseImmersiveDarkMode = 20;
    private const int CaptionColor = 35;

    public static void UseDarkTitleBar(IntPtr hwnd, byte r, byte g, byte b)
    {
        var on = 1;
        DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref on, sizeof(int));

        // Windows 11 can match the caption to the sidebar exactly; Windows 10 ignores this and stays dark.
        var colorRef = r | (g << 8) | (b << 16);
        DwmSetWindowAttribute(hwnd, CaptionColor, ref colorRef, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
