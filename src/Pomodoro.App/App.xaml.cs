using System.Windows;

namespace Pomodoro.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;
    private SystemSleepGuard? _sleepGuard;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstanceCoordinator();
        if (!_singleInstance.IsPrimary)
        {
            _singleInstance.SignalPrimary();
            Shutdown();
            return;
        }

        _sleepGuard = new SystemSleepGuard();
        _mainWindow = new MainWindow();
        MainWindow = _mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        _singleInstance.ActivationRequested += (_, _) =>
            _mainWindow.Dispatcher.BeginInvoke(_mainWindow.BringToFront);
        _mainWindow.Show();
        _singleInstance.StartListening();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _sleepGuard?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
