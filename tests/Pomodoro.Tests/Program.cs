using Pomodoro.Core;

var failures = new List<string>();

Check("Inicio de hora", At(14, 0), PomodoroPhase.Work, 1500, "25:00");
Check("Ejemplo 14:15", At(14, 15), PomodoroPhase.Work, 600, "10:00");
Check("Ultimo segundo de trabajo", At(14, 24, 59), PomodoroPhase.Work, 1, "00:01");
Check("Inicio de descanso", At(14, 25), PomodoroPhase.Rest, 300, "05:00");
Check("Ejemplo 14:27", At(14, 27), PomodoroPhase.Rest, 180, "03:00");
Check("Segunda media hora", At(14, 30), PomodoroPhase.Work, 1500, "25:00");
Check("Segundo descanso", At(14, 55), PomodoroPhase.Rest, 300, "05:00");
Check("Fin de hora", At(14, 59, 59), PomodoroPhase.Rest, 1, "00:01");
Check("Milisegundos redondeados", At(14, 24, 59, 500), PomodoroPhase.Work, 1, "00:01");
Check("Cambio de dia", new DateTimeOffset(2026, 6, 23, 0, 0, 0, TimeSpan.FromHours(2)), PomodoroPhase.Work, 1500, "25:00");
Check("Otro huso horario", new DateTimeOffset(2026, 6, 22, 14, 25, 0, TimeSpan.FromHours(-5)), PomodoroPhase.Rest, 300, "05:00");

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Fallaron {failures.Count} pruebas:");
    failures.ForEach(Console.Error.WriteLine);
    return 1;
}

Console.WriteLine("11 pruebas superadas.");
return 0;

DateTimeOffset At(int hour, int minute, int second = 0, int millisecond = 0) =>
    new(2026, 6, 22, hour, minute, second, millisecond, TimeSpan.FromHours(2));

void Check(string name, DateTimeOffset time, PomodoroPhase phase, int seconds, string countdown)
{
    var actual = PomodoroClock.At(time);
    if (actual.Phase != phase || actual.RemainingSeconds != seconds || actual.Countdown != countdown)
    {
        failures.Add($"{name}: esperado {phase} {seconds} {countdown}, obtenido {actual.Phase} {actual.RemainingSeconds} {actual.Countdown}");
    }
}

