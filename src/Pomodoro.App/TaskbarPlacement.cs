using System.Runtime.InteropServices;
using System.Text;

namespace Pomodoro.App;

// Explorer window classes are an undocumented integration point. Never modify Explorer's
// layout or inject code: loss of this window always falls back to the normal app window.
internal static class TaskbarPlacement
{
    internal enum Result { Visible, Hidden, Unavailable }

    internal static Result Place(nint widget, int offset)
    {
        var bar = FindWindow("Shell_TrayWnd", null);
        if (bar == 0 || !GetWindowRect(bar, out var bounds)) return Result.Unavailable;
        var tray = FindWindowEx(bar, 0, "TrayNotifyWnd", null);
        if (tray == 0 || !GetWindowRect(tray, out var trayBounds)) return Result.Unavailable;
        // The current layout supports a horizontal primary taskbar only.
        if (bounds.Right - bounds.Left < bounds.Bottom - bounds.Top) return Result.Unavailable;
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
            ShowWindow(widget, 0);
            return Result.Hidden;
        }
        var scale = GetDpiForWindow(bar) / 96.0;
        var width = (int)Math.Round(126 * scale);
        var height = Math.Min((int)Math.Round(36 * scale), bounds.Bottom - bounds.Top);
        var x = Math.Max(bounds.Left, trayBounds.Left - width - (int)Math.Round((offset + 8) * scale));
        var y = bounds.Top + (bounds.Bottom - bounds.Top - height) / 2;
        // Ownership makes the widget follow Explorer's z-order without becoming an
        // Explorer child (which could be destroyed when Explorer restarts).
        SetWindowLongPtr(widget, -8, bar);
        if (!SetWindowPos(widget, new nint(-1), x, y, width, height, 0x0010 | 0x0040)) return Result.Unavailable;
        return Result.Visible;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string className, string? name);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int capacity);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
}
