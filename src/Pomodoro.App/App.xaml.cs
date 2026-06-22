using System.Windows;

namespace Pomodoro.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;
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
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
