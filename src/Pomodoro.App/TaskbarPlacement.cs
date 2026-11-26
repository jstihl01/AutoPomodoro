using System.Runtime.InteropServices;
using System.Text;

namespace Pomodoro.App;

// Explorer window classes are an undocumented integration point. Never modify Explorer's
// layout or inject code: loss of this window always falls back to the normal app window.
internal static class TaskbarPlacement
{
    private const int WidthDip = 146;
    private const int HeightDip = 38;
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;
    private const uint Show = 0x0040;

    internal enum Result { Visible, Hidden, Unavailable }

    internal static Result Place(nint widget, int offset, ref nint lastBar) =>
        Place(widget, FindWindow("Shell_TrayWnd", null), 0, offset, ref lastBar);

    internal static Result Place(nint widget, nint bar, nint tray, int offset, ref nint lastBar)
    {
        if (widget == 0) return Result.Unavailable;
        if (bar == 0 || !GetWindowRect(bar, out var bounds)) return Result.Unavailable;
        // The current layout supports a horizontal primary taskbar only.
        if (bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top ||
            bounds.Right - bounds.Left < bounds.Bottom - bounds.Top) return Result.Unavailable;
        var monitor = MonitorFromWindow(bar, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return Result.Unavailable;
        var foreground = GetForegroundWindow();
        var className = new StringBuilder(256);
        GetClassName(foreground, className, className.Capacity);
        bool fullscreen = foreground != 0 && foreground != widget && foreground != bar &&
            className.ToString() is not ("Progman" or "WorkerW") &&
            MonitorFromWindow(foreground, 2) == monitor && GetWindowRect(foreground, out var front) &&
            front.Left <= info.Monitor.Left && front.Top <= info.Monitor.Top &&
            front.Right >= info.Monitor.Right && front.Bottom >= info.Monitor.Bottom;
        var visibleHeight = Math.Min(bounds.Bottom, info.Monitor.Bottom) - Math.Max(bounds.Top, info.Monitor.Top);
        if (!IsWindowVisible(bar) || visibleHeight < 8 || fullscreen)
        {
            if (IsWindowVisible(widget)) ShowWindow(widget, 0);
            lastBar = 0;
            return Result.Hidden;
        }
        if (tray == 0) tray = FindWindowEx(bar, 0, "TrayNotifyWnd", null);
        if (tray == 0 || !GetWindowRect(tray, out var trayBounds)) return Result.Unavailable;
        var dpi = GetDpiForWindow(bar);
        if (!IsWindow(tray) || !TryGetBounds(bounds, trayBounds, dpi, offset, out var target))
            return Result.Unavailable;

        // Explorer supplies coordinates only. Raise the widget on first show or recovery.
        var raise = lastBar != bar || !IsWindowVisible(widget);
        if (raise || !GetWindowRect(widget, out var current) || current != target)
        {
            var flags = NoActivate | (raise ? Show : NoZOrder);
            if (!SetWindowPos(widget, raise ? new nint(-1) : 0, target.Left, target.Top,
                    target.Right - target.Left, target.Bottom - target.Top, flags))
                return Result.Unavailable;
        }
        lastBar = bar;
        return Result.Visible;
    }

    internal static bool TryGetBounds(Rect bar, Rect tray, uint dpi, int offset, out Rect bounds)
    {
        bounds = default;
        if (dpi == 0 || bar.Right <= bar.Left || bar.Bottom <= bar.Top ||
            tray.Right <= tray.Left || tray.Bottom <= tray.Top ||
            tray.Left <= bar.Left || tray.Left >= bar.Right ||
            tray.Top >= bar.Bottom || tray.Bottom <= bar.Top)
            return false;

        var scale = dpi / 96.0;
        var width = (int)Math.Round(WidthDip * scale);
        var height = Math.Min((int)Math.Round(HeightDip * scale), bar.Bottom - bar.Top);
        if (width <= 0 || height < (int)Math.Round(28 * scale) || tray.Left - bar.Left < width) return false;
        var left = Math.Max(bar.Left, tray.Left - width - (int)Math.Round((offset + 8) * scale));
        var top = bar.Top + (bar.Bottom - bar.Top - height) / 2;
        bounds = new Rect(left, top, left + width, top + height);
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Rect(int Left, int Top, int Right, int Bottom);
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string className, string? name);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int capacity);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
}
