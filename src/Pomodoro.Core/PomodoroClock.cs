namespace Pomodoro.Core;

public enum PomodoroPhase
{
    Work,
    Rest
}

public enum PomodoroMode
{
    Standard25_5,
    Extended50_10
}

public readonly record struct PomodoroSnapshot(PomodoroPhase Phase, int RemainingSeconds)
{
    public string Countdown => $"{RemainingSeconds / 60:00}:{RemainingSeconds % 60:00}";
    public string Label => Phase == PomodoroPhase.Work ? "TRABAJO" : "DESCANSO";
}

public static class PomodoroClock
{
    public static PomodoroSnapshot At(DateTimeOffset localTime, PomodoroMode mode = PomodoroMode.Standard25_5) =>
        Read(localTime, mode).Snapshot;

    public static (PomodoroSnapshot Snapshot, DateTimeOffset PhaseStart) Read(DateTimeOffset localTime, PomodoroMode mode)
    {
        var work = (mode == PomodoroMode.Standard25_5 ? 25 : 50) * TimeSpan.TicksPerMinute;
        var cycle = (mode == PomodoroMode.Standard25_5 ? 30 : 60) * TimeSpan.TicksPerMinute;
        var elapsed = localTime.TimeOfDay.Ticks % cycle;
        var isWork = elapsed < work;
        var remaining = (isWork ? work : cycle) - elapsed;
        var roundedSeconds = (int)((remaining + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond);
        return (new PomodoroSnapshot(isWork ? PomodoroPhase.Work : PomodoroPhase.Rest, roundedSeconds),
            localTime.AddTicks((isWork ? 0 : work) - elapsed));
    }
}
