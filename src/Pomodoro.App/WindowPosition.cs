using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Pomodoro.App;

internal static class WindowPosition
{
    private const double MinWidth = 220;
    private const double MinHeight = 180;
    private const double MaxSize = 4000;
    private const double DefaultSize = 320;

    public static WindowGeometry Default()
    {
        var area = SystemParameters.WorkArea;
        return new WindowGeometry(area.Right - DefaultSize - 24, area.Top + 24, DefaultSize, DefaultSize, SystemScale);
    }

    public static WindowGeometry Capture(Window window)
    {
        var bounds = window.RestoreBounds;
        return new WindowGeometry(bounds.Left, bounds.Top, bounds.Width, bounds.Height, CurrentScale(window));
    }

    public static void Restore(Window window, WindowGeometry geometry)
    {
        var areas = new List<Rect>();
        EnumDisplayMonitors(0, 0, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info) && info.Work.Right > info.Work.Left && info.Work.Bottom > info.Work.Top)
                areas.Add(new Rect(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top));
            return true;
        }, 0);
        var scale = CurrentScale(window);
        if (areas.Count == 0)
        {
            var work = SystemParameters.WorkArea;
            areas.Add(new Rect(work.Left * SystemScale, work.Top * SystemScale, work.Width * SystemScale, work.Height * SystemScale));
        }

        var fit = Fit(IsValid(geometry) ? geometry : Default(), SystemScale, scale, areas);
        window.Width = fit.Width;
        window.Height = fit.Height;
        window.Left = fit.Left;
        window.Top = fit.Top;
    }

    internal static bool IsValid(WindowGeometry geometry) =>
        double.IsFinite(geometry.Left) && double.IsFinite(geometry.Top) &&
        Math.Abs(geometry.Left) < int.MaxValue / 10d && Math.Abs(geometry.Top) < int.MaxValue / 10d &&
        geometry.Width is >= MinWidth and <= MaxSize && geometry.Height is >= MinHeight and <= MaxSize &&
        (geometry.PositionScale is null or (>= 0.5 and <= 10));

    internal static WindowGeometry Fit(WindowGeometry geometry, double systemScale, double scale, IReadOnlyList<Rect> pixelAreas)
    {
        // WPF positions use the window's scale. Convert every work area to that same coordinate space.
        var savedScale = geometry.PositionScale ?? systemScale;
        var left = geometry.Left * savedScale / scale;
        var top = geometry.Top * savedScale / scale;
        var grip = new Rect(left + geometry.Width - 88, top, 88, 48);
        var areas = pixelAreas.Select(a => new Rect(a.Left / scale, a.Top / scale, a.Width / scale, a.Height / scale)).ToArray();
        if (areas.Any(area => area.Contains(grip)))
            return geometry with { Left = left, Top = top, PositionScale = scale };

        var point = new Point(grip.Left + grip.Width / 2, grip.Top + grip.Height / 2);
        var nearest = areas.MinBy(area => DistanceSquared(point, area));
        var width = Math.Clamp(geometry.Width, MinWidth, Math.Max(MinWidth, nearest.Width));
        var height = Math.Clamp(geometry.Height, MinHeight, Math.Max(MinHeight, nearest.Height));
        return new WindowGeometry(
            Math.Clamp(left, nearest.Left, Math.Max(nearest.Left, nearest.Right - width)),
            Math.Clamp(top, nearest.Top, Math.Max(nearest.Top, nearest.Bottom - height)),
            width, height, scale);
    }

    private static double DistanceSquared(Point p, Rect area)
    {
        var x = p.X - Math.Clamp(p.X, area.Left, area.Right);
        var y = p.Y - Math.Clamp(p.Y, area.Top, area.Bottom);
        return x * x + y * y;
    }

    private static double SystemScale => Math.Max(96u, GetDpiForSystem()) / 96.0;
    private static double CurrentScale(Window window) =>
        PresentationSource.FromVisual(window) is null ? SystemScale : VisualTreeHelper.GetDpi(window).DpiScaleX;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    private delegate bool MonitorEnumProc(nint monitor, nint dc, nint rect, nint data);
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorEnumProc callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
