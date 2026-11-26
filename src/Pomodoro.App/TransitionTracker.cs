using Pomodoro.Core;

namespace Pomodoro.App;

internal readonly record struct ClockSample(DateTimeOffset WallTime, TimeSpan AwakeTime);

internal sealed class TransitionTracker
{
    private static readonly TimeSpan AlertWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromMilliseconds(500);
    private Observation? _previous;
    private (DateTimeOffset Start, PomodoroMode Mode)? _lastAlert;

    public void Resynchronize() => _previous = null;

    public (PomodoroSnapshot Snapshot, bool Alert) Update(ClockSample sample, PomodoroMode mode)
    {
        var now = sample.WallTime;
        var (snapshot, start) = PomodoroClock.Read(now, mode);
        var previous = _previous;
        _previous = new Observation(sample, mode, start);

        if (previous is not { } prior || prior.Mode != mode || now.Offset != prior.Sample.WallTime.Offset ||
            prior.Sample.WallTime >= start || now - start > AlertWindow || _lastAlert == (start, mode))
            return (snapshot, false);

        // Compare the two clocks to distinguish a stalled UI from suspension or a clock jump.
        var awakeGap = sample.AwakeTime - prior.Sample.AwakeTime;
        var wallGap = now - prior.Sample.WallTime;
        if (awakeGap < TimeSpan.Zero || (wallGap - awakeGap).Duration() > ClockTolerance ||
            PomodoroClock.Read(start.AddTicks(-1), mode).PhaseStart != prior.PhaseStart)
            return (snapshot, false);

        _lastAlert = (start, mode);
        return (snapshot, true);
    }

    private readonly record struct Observation(ClockSample Sample, PomodoroMode Mode, DateTimeOffset PhaseStart);
}
