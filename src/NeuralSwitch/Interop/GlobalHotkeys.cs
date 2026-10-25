using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace NeuralSwitch.Interop;

/// <summary>System-wide hotkeys, so the game can keep focus while Neural Rendering is switched.</summary>
public sealed class GlobalHotkeys : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = [];
    private int _nextId = 0x4E53; // "NS"

    public GlobalHotkeys(IntPtr windowHandle)
    {
        _source = HwndSource.FromHwnd(windowHandle);
        _source.AddHook(Hook);
    }

    /// <summary>Registers Ctrl+Alt+<paramref name="key"/>. False when another program already has it.</summary>
    public bool RegisterCtrlAlt(char key, Action action)
    {
        var id = _nextId++;

        if (!RegisterHotKey(_source.Handle, id, ModControl | ModAlt | ModNoRepeat, char.ToUpperInvariant(key)))
            return false;

        _actions[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys)
            UnregisterHotKey(_source.Handle, id);

        _actions.Clear();
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(Hook);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
