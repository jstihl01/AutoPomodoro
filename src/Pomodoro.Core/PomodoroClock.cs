namespace Pomodoro.Core;

public enum PomodoroPhase
{
    Work,
    Rest
}

public readonly record struct PomodoroSnapshot(PomodoroPhase Phase, int RemainingSeconds)
{
    public string Countdown => $"{RemainingSeconds / 60:00}:{RemainingSeconds % 60:00}";
    public string Label => Phase == PomodoroPhase.Work ? "TRABAJO" : "DESCANSO";
}

public static class PomodoroClock
{
    private const double WorkSeconds = 25 * 60;
    private const double CycleSeconds = 30 * 60;

    public static PomodoroSnapshot At(DateTimeOffset localTime)
    {
        var seconds = (localTime.Minute % 30 * 60)
            + localTime.Second
            + localTime.Millisecond / 1000d
            + localTime.Microsecond / 1_000_000d;

        if (seconds < WorkSeconds)
        {
            return new PomodoroSnapshot(
                PomodoroPhase.Work,
                Math.Max(1, (int)Math.Ceiling(WorkSeconds - seconds)));
        }

        return new PomodoroSnapshot(
            PomodoroPhase.Rest,
            Math.Max(1, (int)Math.Ceiling(CycleSeconds - seconds)));
    }
}

