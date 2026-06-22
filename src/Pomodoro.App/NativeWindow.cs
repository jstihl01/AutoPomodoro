using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Pomodoro.App;

internal static class NativeWindow
{
    private const int DwmWindowCornerPreference = 33;
    private const int Round = 2;

    public static void EnableRoundedCorners(System.Windows.Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var preference = Round;
        DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref preference, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}

