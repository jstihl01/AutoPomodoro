using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace Pomodoro.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;
    private SystemSleepGuard? _sleepGuard;
    private MainWindow? _mainWindow;
    private bool _subscribed;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args is ["--runtime-info"])
        {
            _ = NativeWindow.ReadClock();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Version = typeof(App).Assembly.GetName().Version?.ToString(3),
                RuntimeVersion = Environment.Version.ToString(),
                WindowsDesktopVersion = typeof(Window).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion.Split('+')[0],
                Architecture = RuntimeInformation.ProcessArchitecture.ToString()
            }));
            Shutdown();
            return;
        }

        try
        {
            _singleInstance = new SingleInstanceCoordinator();
            if (!_singleInstance.AcquireOrActivate())
            {
                Shutdown();
                return;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or TimeoutException or WaitHandleCannotBeOpenedException)
        {
            Trace.TraceWarning("AutoPomodoro: inicio de instancia ({0}, 0x{1:X8})", error.GetType().Name, error.HResult);
            MessageBox.Show("No se pudo iniciar o activar AutoPomodoro. Si ya está abierto, cierra la otra instancia y vuelve a intentarlo.",
                "AutoPomodoro", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
            return;
        }

        _sleepGuard = new SystemSleepGuard();
        _sleepGuard.TryAcquire();
        _mainWindow = new MainWindow();
        MainWindow = _mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        _mainWindow.SetPowerWarning(!_sleepGuard.Active);
        SystemEvents.TimeChanged += TimeChanged;
        SystemEvents.PowerModeChanged += PowerChanged;
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        SystemEvents.UserPreferenceChanged += PreferencesChanged;
        _subscribed = true;
        _mainWindow.ShowSavedView();
        try
        {
            _singleInstance.StartListening(token =>
                Dispatcher.InvokeAsync(_mainWindow.BringToFront, System.Windows.Threading.DispatcherPriority.Normal, token).Task);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("AutoPomodoro: no se pudo abrir el canal de activación (0x{0:X8})", error.HResult);
            MessageBox.Show("El reloj funciona, pero el acceso directo no puede recuperar esta instancia.",
                "AutoPomodoro", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void TimeChanged(object? sender, EventArgs e) => Dispatch(RefreshLocalClock);

    private void PowerChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume) return;
        Dispatch(() =>
        {
            _sleepGuard?.TryAcquire();
            _mainWindow?.SetPowerWarning(_sleepGuard?.Active != true);
            RefreshLocalClock();
        });
    }

    private void PreferencesChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.Locale) Dispatch(RefreshLocalClock);
        if (e.Category == UserPreferenceCategory.Desktop) DisplayChanged(sender, e);
    }

    private void DisplayChanged(object? sender, EventArgs e) => Dispatch(() => _mainWindow?.RefreshDisplay());

    private void RefreshLocalClock()
    {
        TimeZoneInfo.ClearCachedData();
        _mainWindow?.ResynchronizeClock();
    }

    private void Dispatch(Action action)
    {
        if (!Dispatcher.HasShutdownStarted) Dispatcher.InvokeAsync(action);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_subscribed)
        {
            SystemEvents.TimeChanged -= TimeChanged;
            SystemEvents.PowerModeChanged -= PowerChanged;
            SystemEvents.DisplaySettingsChanged -= DisplayChanged;
            SystemEvents.UserPreferenceChanged -= PreferencesChanged;
        }
        _sleepGuard?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
