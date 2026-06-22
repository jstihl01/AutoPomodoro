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
    public static PomodoroSnapshot At(DateTimeOffset localTime, PomodoroMode mode = PomodoroMode.Standard25_5)
    {
        var workSeconds = mode == PomodoroMode.Standard25_5 ? 25 * 60d : 50 * 60d;
        var cycleSeconds = mode == PomodoroMode.Standard25_5 ? 30 * 60d : 60 * 60d;
        var cycleMinutes = mode == PomodoroMode.Standard25_5 ? localTime.Minute % 30 : localTime.Minute;
        var seconds = (cycleMinutes * 60)
            + localTime.Second
            + localTime.Millisecond / 1000d
            + localTime.Microsecond / 1_000_000d;

        if (seconds < workSeconds)
        {
            return new PomodoroSnapshot(
                PomodoroPhase.Work,
                Math.Max(1, (int)Math.Ceiling(workSeconds - seconds)));
        }

        return new PomodoroSnapshot(
            PomodoroPhase.Rest,
            Math.Max(1, (int)Math.Ceiling(cycleSeconds - seconds)));
    }
}
