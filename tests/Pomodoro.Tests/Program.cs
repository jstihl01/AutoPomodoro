using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Pomodoro.App;
using Pomodoro.Core;

internal static class Program
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);
    private static readonly WindowGeometry DefaultGeometry = new(90, 60, 320, 320);
    private const PomodoroMode Standard = PomodoroMode.Standard25_5;
    private const PomodoroMode Extended = PomodoroMode.Extended50_10;
    private const PomodoroPhase Work = PomodoroPhase.Work;
    private const PomodoroPhase Rest = PomodoroPhase.Rest;

    [STAThread]
    private static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
            new Uri("/AutoPomodoro;component/Design.xaml", UriKind.Relative)));
        var cases = new List<(string Name, Action Run)>
        {
            ("COR-02 / TST-01: retraso de interfaz, ambos avisos y cambio de modo", CheckWindowClock),
            ("COR-02 / COR-04: saltos, suspensión, huso y deduplicación", CheckClockDiscontinuities),
            ("COR-03: hueco entre monitores y coordenadas con DPI distinto", CheckMonitorGeometry),
            ("COR-03: recuperar una ventana WPF fuera del escritorio", CheckOffscreenWindow),
            ("COR-06: migrar volumen audible y respaldo de perfil silenciado", CheckMigration),
            ("FIA-03: una lectura bloqueada no reemplaza los datos al abrir y cerrar", CheckLockedRead),
            ("FIA-03: recuperar legado conservando el archivo ilegible", CheckRecovery),
            ("FIA-05 / FIA-07: guardar con geometría inválida y reintentar un reemplazo bloqueado", CheckSave),
            ("FIA-07: fallo visible y reintento desde Ajustes", CheckPendingSettings),
            ("SEG-05: preferencia sobredimensionada y reemplazo explícito", CheckOversized),
            ("FIA-01 / FIA-09: colocación con la cola de la barra sin bombeo", CheckIndependentWidget),
            ("FIA-09: rechazar DPI cero, rectángulos inválidos y espacio insuficiente", CheckPlacementData),
            ("COR-01 / COR-05 / TST-01: arranque, pérdida, recuperación y salida del widget", CheckViewLifecycle),
            ("UX-01 / UX-02 / UX-03 / UX-04: teclado, fase textual y UI Automation", CheckWidgetAccessibility),
            ("ARQ-01: aplicar el restablecimiento completo al cerrar Ajustes", CheckReset),
            ("FIA-06 / FIA-10: fallos visibles de sonido y energía", CheckWarnings),
            ("FIA-11: cola acotada, prioridad de avisos y cancelación de prueba", CheckAudioQueue),
            ("FIA-08 / SEG-04: activación confirmada y relanzamiento simultáneo al cierre", CheckActivation)
        };

        var fractionalOffset = TimeSpan.FromMinutes(345);
        var clockCases = new (DateTimeOffset Time, PomodoroMode Mode, PomodoroPhase Phase, int Seconds, string Text)[]
        {
            (At(14, 0), Standard, Work, 1500, "25:00"),
            (At(14, 15), Standard, Work, 600, "10:00"),
            (At(14, 24, 59), Standard, Work, 1, "00:01"),
            (At(14, 25), Standard, Rest, 300, "05:00"),
            (At(14, 27), Standard, Rest, 180, "03:00"),
            (At(14, 30), Standard, Work, 1500, "25:00"),
            (At(14, 55), Standard, Rest, 300, "05:00"),
            (At(14, 59, 59), Standard, Rest, 1, "00:01"),
            (At(14, 24, 59, 500), Standard, Work, 1, "00:01"),
            (At(0, 0), Standard, Work, 1500, "25:00"),
            (At(14, 25, offset: fractionalOffset), Standard, Rest, 300, "05:00"),
            (At(14, 50, offset: fractionalOffset), Extended, Rest, 600, "10:00"),
            (At(14, 24, 58, 500), Standard, Work, 2, "00:02"),
            (At(14, 29, 58, 500), Standard, Rest, 2, "00:02"),
            (At(14, 0), Extended, Work, 3000, "50:00"),
            (At(14, 25), Extended, Work, 1500, "25:00"),
            (At(14, 49, 59), Extended, Work, 1, "00:01"),
            (At(14, 50), Extended, Rest, 600, "10:00"),
            (At(14, 59, 59), Extended, Rest, 1, "00:01"),
            (At(15, 0), Extended, Work, 3000, "50:00")
        };
        foreach (var (time, mode, phase, seconds, text) in clockCases)
        {
            cases.Add(($"Reloj {mode} {time:HH:mm:ss.fff zzz}", () =>
                Require(PomodoroClock.At(time, mode) == new PomodoroSnapshot(phase, seconds) &&
                        PomodoroClock.At(time, mode).Countdown == text, $"Se esperaba {phase} {text}")));
        }

        var executed = 0;
        var failed = 0;
        foreach (var (name, run) in cases)
        {
            executed++;
            try { run(); Console.WriteLine($"OK {name}"); }
            catch (Exception error) { failed++; Console.Error.WriteLine($"FALLO {name}: {error}"); }
        }
        Console.WriteLine($"{executed - failed}/{executed} pruebas superadas.");
        return executed != 0 && failed == 0 ? 0 : 1;
    }

    private static void CheckWindowClock()
    {
        using var profile = new Profile();
        var sounds = new ConcurrentQueue<SoundKind>();
        var sample = new ClockSample(At(14, 24, 58), TimeSpan.Zero);
        WithWindow(profile, window =>
        {
            window.ShowSavedView();
            sample = new ClockSample(At(14, 25, 0, 100), TimeSpan.FromMilliseconds(2100));
            window.UpdateTimer();
            PumpUntil(() => sounds.Count == 1);
            Require(window.CountdownText.Text == "05:00", "El contador no cruzó la frontera.");
            window.UpdateTimer();
            window.ApplySettings(60, Extended, reset: false);
            Require(window.PhaseText.Text == "TRABAJO" && sounds.Count == 1, "Cambiar de modo debe ser silencioso.");
            sample = new ClockSample(At(14, 59, 57), TimeSpan.FromSeconds(2099));
            window.UpdateTimer();
            sample = new ClockSample(At(15, 0, 0, 100), TimeSpan.FromSeconds(2102.1));
            window.UpdateTimer();
            PumpUntil(() => sounds.Count == 2);
            Require(sounds.SequenceEqual([SoundKind.WorkFinished, SoundKind.RestFinished]), "Avisos de fase incorrectos.");
        }, clock: () => sample, playback: (kind, _) => { sounds.Enqueue(kind); return true; });
    }

    private static void CheckClockDiscontinuities()
    {
        var tracker = new TransitionTracker();
        bool Observe(DateTimeOffset wall, double awake) => tracker.Update(new ClockSample(wall, TimeSpan.FromSeconds(awake)), Standard).Alert;

        Require(!Observe(At(14, 24, 58), 0), "Debe arrancar en silencio.");
        Require(!Observe(At(14, 25, 0, 100), 0.1), "No debe avisar tras suspensión.");
        Require(!Observe(At(14, 29, 59), 299), "No debe avisar en una fase avanzada.");
        Require(Observe(At(14, 30, 0, 100), 300.1), "Faltó la transición normal.");
        tracker.Resynchronize();
        Require(!Observe(At(14, 29, 59), 301), "No debe avisar al retroceder el reloj.");
        Require(!Observe(At(14, 30, 0, 100), 302.1), "La misma frontera se anunció otra vez.");
        Require(!Observe(At(14, 55, 0, 100), 302.2), "El salto manual no debe avisar.");

        tracker.Resynchronize();
        var before = At(14, 25, offset: TimeSpan.FromHours(2));
        Require(tracker.Update(new ClockSample(before, TimeSpan.Zero), Extended).Snapshot.Phase == Work, "Fase inicial incorrecta.");
        var local = before.AddMilliseconds(100).ToOffset(TimeSpan.FromHours(2.5));
        var result = tracker.Update(new ClockSample(local, TimeSpan.FromMilliseconds(100)), Extended);
        Require(result.Snapshot.Phase == Rest && result.Snapshot.Countdown == "05:00" && !result.Alert,
            "El cambio de huso debe cambiar la vista en silencio.");
        var native = NativeWindow.ReadClock();
        Require(native.AwakeTime > TimeSpan.Zero && native.WallTime.Offset == DateTimeOffset.Now.Offset,
            "La lectura de tiempo activo y hora local de Windows no coincide.");
    }

    private static void CheckMonitorGeometry()
    {
        Rect[] areas = [new(0, 0, 1200, 800), new(1200, 700, 1200, 800)];
        var gap = new WindowGeometry(1400, 50, 320, 320);
        var fit = WindowPosition.Fit(gap, 1, 1, areas);
        Require(areas.Any(area => area.Contains(new Rect(fit.Left + fit.Width - 88, fit.Top, 88, 48))), "Controles fuera de pantalla.");
        Require(fit != gap, "Se aceptó el hueco del escritorio virtual.");
        var valid = gap with { Left = 100, Top = 100, PositionScale = 1.5 };
        var before = WindowPosition.Fit(valid, 1, 1, areas);
        var after = WindowPosition.Fit(valid, 1, 1.5, areas);
        Require(before.Left == 150 && after.Left == 100 && before.Width == after.Width, "La posición física cambió con el DPI.");
    }

    private static void CheckMigration()
    {
        using var profile = new Profile();
        File.WriteAllText(profile.Legacy, """{"Left":90,"Top":60,"Width":320,"Height":320,"VolumePercent":20}""");
        var store = profile.Store();
        var loaded = store.Load();
        Require(loaded.Migrate && loaded.Settings.LastAudibleVolume == 20, "No se migró el último volumen audible.");
        Require(store.Save(loaded.Settings) == SettingsSaveResult.Saved, "No se pudo migrar.");
        Require(File.Exists(profile.Legacy) && File.Exists(profile.Current), "La migración debe dejar el original intacto.");
        Require(profile.Store().Load().Settings.WithVolume(0).LastAudibleVolume == 20, "Silenciar cambió el volumen audible.");
        File.WriteAllText(profile.Current, """{"VolumePercent":0,"LastAudibleVolume":37}""");
        Require(profile.Store().Load().Settings.LastAudibleVolume == 37, "Se perdió el respaldo de un perfil silenciado.");
    }

    private static void CheckOffscreenWindow()
    {
        using var profile = new Profile();
        var store = profile.Store();
        var settings = store.Defaults() with { Geometry = new WindowGeometry(1_000_000, 1_000_000, 320, 320) };
        Require(store.Save(settings) == SettingsSaveResult.Saved, "No se guardó la geometría de prueba.");
        WithWindow(profile, main =>
        {
            main.ShowSavedView();
            Require(main.IsVisible && main.Left < 1_000_000 && main.Top < 1_000_000,
                "La ventana quedó fuera del escritorio.");
        });
    }

    private static void CheckLockedRead()
    {
        using var profile = new Profile();
        const string original = """{"VolumePercent":27,"Mode":1}""";
        File.WriteAllText(profile.Current, original);
        MainWindow? window = null;
        using (File.Open(profile.Current, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            window = NewWindow(profile);
            window.ShowSavedView();
        }
        window.Close();
        Require(File.ReadAllText(profile.Current) == original, "El cierre sobrescribió las preferencias.");
    }

    private static void CheckRecovery()
    {
        using var profile = new Profile();
        File.WriteAllText(profile.Current, "{datos incompletos");
        File.WriteAllText(profile.Legacy, """{"VolumePercent":21,"Mode":1}""");
        var store = profile.Store();
        var loaded = store.Load();
        Require(loaded.ReadWarning && loaded.Settings.VolumePercent == 21, "No se recuperó la copia heredada.");
        Require(store.Save(loaded.Settings) == SettingsSaveResult.ReadProtected, "Se debe proteger el archivo sin edición.");
        Require(store.Save(loaded.Settings.WithVolume(22), explicitChange: true) == SettingsSaveResult.Saved, "No se guardó la edición recuperada.");
        var copy = Directory.GetFiles(Path.GetDirectoryName(profile.Current)!, "*.recovered").Single();
        Require(File.ReadAllText(copy) == "{datos incompletos", "Se perdió el original.");
        Require(profile.Store().Load().Settings.VolumePercent == 22, "No persistió el volumen recuperado.");
    }

    private static void CheckSave()
    {
        using var profile = new Profile();
        var store = profile.Store();
        var defaults = store.Load().Settings;
        Require(store.Save(defaults) == SettingsSaveResult.Saved, "No se pudieron crear los ajustes.");
        var edited = defaults.WithVolume(39) with { Mode = Extended, Geometry = defaults.Geometry with { Width = 5000 } };
        using (File.Open(profile.Current, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Require(store.Save(edited) == SettingsSaveResult.Failed, "No se informó el fallo de guardado.");
            Require(Directory.GetFiles(Path.GetDirectoryName(profile.Current)!, "*.tmp").Length == 0, "Quedó un temporal.");
        }
        Require(store.Save(edited) == SettingsSaveResult.Saved, "El reintento no guardó los cambios.");
        var saved = profile.Store().Load().Settings;
        Require(saved.Mode == Extended && saved.VolumePercent == 39 && saved.Geometry == defaults.Geometry, "El tamaño inválido descartó preferencias.");
        File.WriteAllText(profile.Current, "{");
        var fromBackup = profile.Store().Load();
        Require(fromBackup.ReadWarning && fromBackup.Settings.VolumePercent == defaults.VolumePercent, "No se leyó la copia de respaldo.");
    }

    private static void CheckOversized()
    {
        using var profile = new Profile();
        File.WriteAllBytes(profile.Current, new byte[SettingsStore.MaximumFileBytes + 1]);
        var store = profile.Store();
        var loaded = store.Load();
        Require(loaded.ReadWarning && !loaded.Migrate, "El archivo grande se aceptó.");
        Require(store.Save(loaded.Settings.WithVolume(80), explicitChange: true) == SettingsSaveResult.ReadProtected, "Se sobrescribió sin base válida.");
        Require(store.Save(loaded.Settings, reset: true) == SettingsSaveResult.Saved, "No se respetó el restablecimiento explícito.");
        Require(new FileInfo(Directory.GetFiles(Path.GetDirectoryName(profile.Current)!, "*.recovered").Single()).Length ==
                SettingsStore.MaximumFileBytes + 1, "El archivo anterior no se conservó.");
    }

    private static void CheckPendingSettings()
    {
        using var profile = new Profile();
        var store = profile.Store();
        Require(store.Save(store.Defaults()) == SettingsSaveResult.Saved, "No se pudieron preparar los ajustes.");
        WithWindow(profile, main =>
        {
            main.ShowSavedView();
            using (File.Open(profile.Current, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                main.ApplySettings(41, Extended, reset: false);
                Require(main.StatusText.Text.Contains("Ajustes pendientes"), "El fallo de guardado no se mostró.");
            }
            Invoke<Button>(main, "OpenSettings");
            var settings = Application.Current.Windows.OfType<SettingsWindow>().Single();
            Require(settings.SaveStatus.Text.Contains("Ajustes pendientes"), "No se ofreció el reintento en Ajustes.");
            Invoke<Button>(settings, "SettingsDone");
            Require(main.StatusText.Text.Length == 0 && profile.Store().Load().Settings.VolumePercent == 41,
                "Los cambios pendientes no se guardaron después del reintento.");
        });
    }

    private static void CheckIndependentWidget()
    {
        using var profile = new Profile();
        SeedWidget(profile);
        using var ready = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        nint barHandle = 0;
        nint trayHandle = 0;
        Exception? barError = null;
        var barThread = new Thread(() =>
        {
            try
            {
                const int visiblePopup = unchecked((int)0x90000000);
                const int visibleChild = 0x50000000;
                using var bar = new HwndSource(new HwndSourceParameters("AutoPomodoro Test Bar")
                {
                    WindowStyle = visiblePopup, PositionX = 20, PositionY = 280, Width = 520, Height = 48
                });
                using var tray = new HwndSource(new HwndSourceParameters("AutoPomodoro Test Tray")
                {
                    ParentWindow = bar.Handle, WindowStyle = visibleChild, PositionX = 400, PositionY = 0, Width = 120, Height = 48
                });
                barHandle = bar.Handle;
                trayHandle = tray.Handle;
                ready.Set();
                release.Wait(TimeSpan.FromSeconds(10)); // No message pump, like a stalled shell window.
            }
            catch (Exception error) { barError = error; ready.Set(); }
        }) { IsBackground = true };
        barThread.SetApartmentState(ApartmentState.STA);
        barThread.Start();
        try
        {
            Require(ready.Wait(Timeout) && barError is null && trayHandle != 0, $"No se creó la barra de prueba. {barError}");
            nint lastBar = 0;
            var placement = TaskbarPlacement.Result.Unavailable;
            var clock = new ClockSample(At(14, 24, 58), TimeSpan.Zero);
            var played = new ConcurrentQueue<SoundKind>();
            WithWindow(profile, main =>
            {
                var started = Stopwatch.StartNew();
                main.ShowSavedView();
                Require(started.Elapsed < Timeout && placement == TaskbarPlacement.Result.Visible && !main.IsVisible,
                    $"No se colocó el widget a tiempo ({placement}).");
                var widget = CurrentWidget();
                Require(GetWindow(new WindowInteropHelper(widget).Handle, 4) != barHandle, "La barra quedó como propietaria.");
                clock = new ClockSample(At(14, 25, 0, 100), TimeSpan.FromSeconds(2.1));
                main.UpdateTimer();
                PumpUntil(() => played.Contains(SoundKind.WorkFinished));
                Require(widget.Countdown.Text == "05:00", "El contador no avanzó con la barra detenida.");
                main.RecoverWindow();
                Require(main.IsVisible && !release.IsSet, "No se recuperó la ventana sin reactivar la barra.");
            }, clock: () => clock, playback: (kind, _) => { played.Enqueue(kind); return true; },
                placement: (widget, offset) => placement = TaskbarPlacement.Place(new WindowInteropHelper(widget).Handle,
                    barHandle, trayHandle, offset, ref lastBar));
        }
        finally
        {
            release.Set();
            Require(barThread.Join(Timeout), "La barra de prueba no se cerró.");
        }
    }

    private static void CheckPlacementData()
    {
        var bar = new TaskbarPlacement.Rect(0, 1030, 1920, 1080);
        var tray = new TaskbarPlacement.Rect(1600, 1030, 1920, 1080);
        Require(!TaskbarPlacement.TryGetBounds(bar, tray, 0, 0, out _), "Se aceptó DPI cero.");
        Require(!TaskbarPlacement.TryGetBounds(bar, tray with { Bottom = tray.Top }, 96, 0, out _), "Se aceptó una barra inválida.");
        Require(!TaskbarPlacement.TryGetBounds(bar, tray with { Left = 80 }, 96, 0, out _), "No cabe el widget.");
        Require(TaskbarPlacement.TryGetBounds(bar, tray, 144, 0, out var target) &&
                target.Right <= tray.Left && target.Right - target.Left == 219, "La colocación escalada es incorrecta.");
    }

    private static void CheckViewLifecycle()
    {
        using var profile = new Profile();
        SeedWidget(profile);
        var available = TaskbarPlacement.Result.Visible;
        WithWindow(profile, main =>
        {
            main.ShowSavedView();
            Require(!main.IsVisible && CurrentWidget().IsVisible, "Apareció la vista incorrecta al arrancar.");
            CurrentWidget().Close();
            PumpUntil(() => main.IsVisible);
            Require(profile.Store().Load().Settings.TaskbarMode, "El cierre inesperado borró la preferencia.");
            main.UseTaskbar();
            available = TaskbarPlacement.Result.Unavailable;
            main.RefreshDisplay();
            Require(main.IsVisible && profile.Store().Load().Settings.TaskbarMode, "No se conservó la vista al perder la barra.");
            main.BringToFront();
            Require(!profile.Store().Load().Settings.TaskbarMode, "La elección explícita no se guardó.");
            available = TaskbarPlacement.Result.Visible;
            main.UseTaskbar();
            var closed = false;
            main.Closed += (_, _) => closed = true;
            SendMessage(new WindowInteropHelper(CurrentWidget()).Handle, 0x0112, 0xF060, 0);
            PumpUntil(() => closed);
            Require(profile.Store().Load().Settings.TaskbarMode, "Salir del widget borró la vista elegida.");
        }, placement: (_, _) => available);

        WithWindow(profile, main =>
        {
            main.ShowSavedView();
            Require(main.IsVisible && profile.Store().Load().Settings.TaskbarMode, "Sin barra disponible debe abrirse la ventana.");
        }, placement: (_, _) => TaskbarPlacement.Result.Unavailable);
    }

    private static void CheckWidgetAccessibility()
    {
        using var profile = new Profile();
        WithWindow(profile, main =>
        {
            main.ShowSavedView();
            main.UseTaskbar();
            var widget = CurrentWidget();
            var speaker = widget.VolumeButton;
            var peer = new ToggleButtonAutomationPeer(speaker);
            var provider = (IToggleProvider)peer.GetPattern(PatternInterface.Toggle);
            Require(provider.ToggleState == ToggleState.Off, "El altavoz no declara su estado inicial.");
            provider.Toggle();
            PumpUntil(() => provider.ToggleState == ToggleState.On && widget.VolumeSlider.Value == 0);
            provider.Toggle();
            PumpUntil(() => provider.ToggleState == ToggleState.Off && widget.VolumeSlider.Value == SettingsStore.DefaultVolumePercent);
            var countdown = new ButtonAutomationPeer(widget.OpenWindowButton);
            Require(widget.Phase.Text == "TRABAJO 25/5" && countdown.GetItemStatus().Contains("restantes"), "Falta fase o tiempo en accesibilidad.");
            Press(speaker, Key.F2);
            PumpUntil(() => widget.VolumePopup.IsOpen && widget.VolumeSlider.IsKeyboardFocused);
            Press(widget.VolumeSlider, Key.Escape);
            Require(!widget.VolumePopup.IsOpen && speaker.IsKeyboardFocused, "Escape no devuelve el foco al altavoz.");
        });
    }

    private static void CheckReset()
    {
        using var profile = new Profile();
        var store = profile.Store();
        Require(store.Save(store.Defaults().WithVolume(31) with { Mode = Extended, TaskbarOffset = 12 }) == SettingsSaveResult.Saved,
            "No se pudieron preparar los ajustes.");
        WithWindow(profile, main =>
        {
            main.ShowSavedView();
            Invoke<Button>(main, "OpenSettings");
            var settings = Application.Current.Windows.OfType<SettingsWindow>().Single();
            Invoke<Button>(settings, "ResetSettings");
            Require(settings.SelectedMode == Standard && settings.VolumePercent == 60 && profile.Store().Load().Settings.TaskbarOffset == 12,
                "Restablecer guardó antes de cerrar el panel.");
            Invoke<Button>(settings, "SettingsDone");
            var saved = profile.Store().Load().Settings;
            Require(saved.Mode == Standard && saved.VolumePercent == 60 && saved.TaskbarOffset == 0 && saved.Geometry == DefaultGeometry,
                "El restablecimiento no guardó todos los campos.");
        });
    }

    private static void CheckWarnings()
    {
        using var profile = new Profile();
        var sample = new ClockSample(At(14, 24, 59), TimeSpan.Zero);
        WithWindow(profile, main =>
        {
            main.ShowSavedView();
            main.SetPowerWarning(true);
            Require(main.StatusText.Text.Contains("mantener el equipo despierto"), "El rechazo de energía quedó oculto.");
            sample = new ClockSample(At(14, 25), TimeSpan.FromSeconds(1));
            main.UpdateTimer();
            PumpUntil(() => main.StatusText.Text.Contains("aviso sonoro"));
            Invoke<Button>(main, "OpenSettings");
            var settings = Application.Current.Windows.OfType<SettingsWindow>().Single();
            Invoke<Button>(settings, "TestVolume");
            PumpUntil(() => settings.TestStatus.Text.Contains("no ha podido reproducir"));
        }, clock: () => sample, playback: (_, _) => false);
    }

    private static void CheckAudioQueue()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var sounds = new ConcurrentQueue<SoundKind>();
        using var player = new TransitionSoundPlayer((kind, _) =>
        {
            sounds.Enqueue(kind);
            if (kind == SoundKind.WorkFinished) { entered.Set(); release.Wait(Timeout); }
            return kind != SoundKind.Test;
        });
        try
        {
            player.PlayTransition(Rest, 50);
            Require(entered.Wait(Timeout), "No comenzó la reproducción.");
            var cancelled = player.PlayTest(50);
            Require(Await(player.PlayTest(50)) == PlaybackResult.Busy, "La cola de pruebas no está acotada.");
            player.CancelTests();
            Require(Await(cancelled) == PlaybackResult.Cancelled, "No se canceló la prueba pendiente.");
            var test = player.PlayTest(50);
            player.PlayTransition(Work, 50);
            release.Set();
            Require(Await(test) == PlaybackResult.Failed, "No se informó el resultado fallido de la prueba.");
            Require(sounds.SequenceEqual([SoundKind.WorkFinished, SoundKind.RestFinished, SoundKind.Test]), "La prueba se reprodujo antes del aviso.");
            Require(Await(player.PlayTest(0)) == PlaybackResult.Muted, "El silencio debe distinguirse de un error.");
        }
        finally { release.Set(); }
    }

    private static void CheckActivation()
    {
        var name = $@"Local\AutoPomodoro-Test-{Guid.NewGuid():N}";
        using (var primary = new SingleInstanceCoordinator(name))
        {
            Require(primary.AcquireOrActivate(), "No se adquirió el bloqueo.");
            var activations = 0;
            primary.StartListening(_ => { Interlocked.Increment(ref activations); return Task.FromResult(true); });
            Require(!Await(Task.Run(() =>
            {
                using var secondary = new SingleInstanceCoordinator(name);
                return secondary.AcquireOrActivate();
            })) && activations == 1, "La segunda instancia no recibió la confirmación.");
        }

        using var ready = new ManualResetEventSlim();
        using var closing = new ManualResetEventSlim();
        var previous = Task.Run(() =>
        {
            using var primary = new SingleInstanceCoordinator(name);
            Require(primary.AcquireOrActivate(), "No se adquirió el bloqueo inicial.");
            ready.Set();
            Require(closing.Wait(Timeout), "No se pidió el cierre.");
        });
        try
        {
            Require(ready.Wait(Timeout), "La primera instancia no arrancó.");
            var next = Task.Run(() =>
            {
                using var secondary = new SingleInstanceCoordinator(name);
                return secondary.AcquireOrActivate();
            });
            Thread.Sleep(250);
            closing.Set();
            Require(Await(next), "El relanzamiento se perdió mientras cerraba la instancia anterior.");
        }
        finally { closing.Set(); Await(previous); }
    }

    private static DateTimeOffset At(int hour, int minute, int second = 0, int milliseconds = 0, TimeSpan? offset = null) =>
        new(2026, 6, 22, hour, minute, second, milliseconds, offset ?? TimeSpan.FromHours(2));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void PumpUntil(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done() && watch.Elapsed < Timeout)
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Thread.Sleep(10);
        }
        Require(done(), "Se agotó el tiempo de espera del Dispatcher.");
    }

    private static T Await<T>(Task<T> task) => task.WaitAsync(Timeout).GetAwaiter().GetResult();
    private static void Await(Task task) => task.WaitAsync(Timeout).GetAwaiter().GetResult();

    private static void SeedWidget(Profile profile)
    {
        var store = profile.Store();
        Require(store.Save(store.Defaults() with { TaskbarMode = true }) == SettingsSaveResult.Saved, "No se pudo guardar la vista inicial.");
    }

    private static MainWindow NewWindow(Profile profile, Func<ClockSample>? clock = null, Func<SoundKind, int, bool>? playback = null,
        Func<TaskbarWidget, int, TaskbarPlacement.Result>? placement = null) =>
        new(profile.Store(), new TransitionSoundPlayer(playback ?? ((_, _) => true)),
            clock ?? (() => new ClockSample(At(14, 15), TimeSpan.Zero)),
            placement ?? ((_, _) => TaskbarPlacement.Result.Visible));

    private static void WithWindow(Profile profile, Action<MainWindow> check, Func<ClockSample>? clock = null,
        Func<SoundKind, int, bool>? playback = null, Func<TaskbarWidget, int, TaskbarPlacement.Result>? placement = null)
    {
        var window = NewWindow(profile, clock, playback, placement);
        var closed = false;
        window.Closed += (_, _) => closed = true;
        try { check(window); }
        finally { if (!closed) window.Close(); }
    }

    private static TaskbarWidget CurrentWidget() => Application.Current.Windows.OfType<TaskbarWidget>().Single();

    private static void Invoke<T>(Window window, string automationId) where T : Button
    {
        var button = Descendants(window).OfType<T>().Single(child => AutomationProperties.GetAutomationId(child) == automationId);
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Press(UIElement element, Key key) =>
        element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(element), Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        });

    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);

    private sealed class Profile : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("autopomodoro-tests-");
        public string Current => Path.Combine(_root.FullName, "AutoPomodoro", "settings.json");
        public string Legacy => Path.Combine(_root.FullName, "Pomodoro", "settings.json");

        public Profile()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Current)!);
            Directory.CreateDirectory(Path.GetDirectoryName(Legacy)!);
        }

        public SettingsStore Store() => new(_root.FullName, () => DefaultGeometry);

        public void Dispose() => _root.Delete(recursive: true);
    }
}
