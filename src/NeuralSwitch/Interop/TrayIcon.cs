using System.Windows;
using NeuralSwitch.Core.Engine;
using Forms = System.Windows.Forms;

namespace NeuralSwitch.Interop;

/// <summary>
/// NeuralSwitch's icon in the notification area: keeps it running, with its hotkeys, while the window
/// is closed, and offers the switches without opening it.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _toggle;
    private readonly Forms.ToolStripMenuItem _styles;

    public TrayIcon(Action open, Action toggle, Action<NrStyle> style, Action quit, Func<(bool Connected, bool Enabled, NrStyle Style, string Game)> state)
    {
        _toggle = new Forms.ToolStripMenuItem("Neural Rendering on", null, (_, _) => toggle());
        _styles = new Forms.ToolStripMenuItem("Style");

        foreach (var s in NrStyles.All)
            _styles.DropDownItems.Add(new Forms.ToolStripMenuItem(s.ToString(), null, (_, _) => style(s)) { Tag = s });

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem("Open NeuralSwitch", null, (_, _) => open()) { Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold) });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_toggle);
        menu.Items.Add(_styles);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Quit", null, (_, _) => quit()));

        // The menu reflects the game as it is the moment it opens.
        menu.Opening += (_, _) =>
        {
            var (connected, enabled, current, game) = state();
            _toggle.Enabled = _styles.Enabled = connected;
            _toggle.Text = connected ? $"Neural Rendering on ({game})" : "Neural Rendering on (no game connected)";
            _toggle.Checked = connected && enabled;

            foreach (Forms.ToolStripMenuItem item in _styles.DropDownItems)
                item.Checked = connected && (NrStyle)item.Tag! == current;
        };

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "NeuralSwitch",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                open();
        };
    }

    public void ShowHint(string title, string text) => _icon.ShowBalloonTip(5000, title, text, Forms.ToolTipIcon.Info);

    private static System.Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/NeuralSwitch.ico"));
        using var stream = resource!.Stream;
        return new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
