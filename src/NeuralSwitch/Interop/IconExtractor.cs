using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NeuralSwitch.Interop;

/// <summary>A game's own icon, read from its executable at the size the list shows it.</summary>
internal static class IconExtractor
{
    /// <summary>The executable's first icon at <paramref name="size"/> pixels, frozen for use on any thread, or null.</summary>
    public static ImageSource? FromExe(string path, int size)
    {
        var icons = new IntPtr[1];
        var ids = new uint[1];

        if (PrivateExtractIcons(path, 0, size, size, icons, ids, 1, 0) == 0 || icons[0] == IntPtr.Zero)
            return null;

        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(icons[0], Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(size, size));
            image.Freeze();
            return image;
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            DestroyIcon(icons[0]);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint PrivateExtractIcons(string file, int index, int cx, int cy, IntPtr[] icons, uint[] ids, uint count, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);
}
