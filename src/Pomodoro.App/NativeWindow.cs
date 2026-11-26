using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Pomodoro.App;

internal static class NativeWindow
{
    private const int DwmWindowCornerPreference = 33;
    private const int Round = 2;

    public static ClockSample ReadClock()
    {
        if (!QueryUnbiasedInterruptTime(out var ticks))
            throw new InvalidOperationException("No se pudo consultar el tiempo activo de Windows.");
        var utc = DateTimeOffset.UtcNow;
        var utcFields = new SystemTime(utc.UtcDateTime);
        // Convert via Windows so the countdown does not depend on the process's cached local zone.
        var localTime = SystemTimeToTzSpecificLocalTimeEx(0, in utcFields, out var local)
            ? utc.ToOffset(local.AsDateTime() - utcFields.AsDateTime())
            : utc.ToLocalTime();
        return new ClockSample(localTime, TimeSpan.FromTicks((long)ticks));
    }

    [DllImport("kernel32.dll")]
    private static extern bool QueryUnbiasedInterruptTime(out ulong ticks);

    [DllImport("kernel32.dll")]
    private static extern bool SystemTimeToTzSpecificLocalTimeEx(nint zone, in SystemTime utc, out SystemTime local);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime(DateTime value)
    {
        private ushort _year = (ushort)value.Year;
        private ushort _month = (ushort)value.Month;
        private ushort _dayOfWeek = (ushort)value.DayOfWeek;
        private ushort _day = (ushort)value.Day;
        private ushort _hour = (ushort)value.Hour;
        private ushort _minute = (ushort)value.Minute;
        private ushort _second = (ushort)value.Second;
        private ushort _milliseconds = (ushort)value.Millisecond;

        public readonly DateTime AsDateTime() =>
            new(_year, _month, _day, _hour, _minute, _second, _milliseconds);
    }

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

