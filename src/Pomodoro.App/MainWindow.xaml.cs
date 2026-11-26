using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Pomodoro.Core;
using MediaColor = System.Windows.Media.Color;

namespace Pomodoro.App;

public partial class MainWindow : Window
{
    private static readonly SolidColorBrush WorkBrush = new(MediaColor.FromRgb(36, 152, 91));
    private static readonly SolidColorBrush ExtendedWorkBrush = new(MediaColor.FromRgb(198, 56, 78));
    private static readonly SolidColorBrush RestBrush = new(MediaColor.FromRgb(30, 107, 211));

    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly TransitionTracker _transitions = new();
    private readonly SettingsStore _store;
    private readonly TransitionSoundPlayer _sounds;
    private readonly Func<ClockSample> _clock;
    private readonly Func<TaskbarWidget, int, TaskbarPlacement.Result>? _placeWidget;
    private AppSettings _settings;
    private AppSettings _savedSettings;
    private WindowGeometry? _displayedGeometry;
    private SettingsWindow? _settingsWindow;
    private TaskbarWidget? _widget;
    private string _settingsProblem = "";
    private bool _needsMigration;
    private bool _powerFailed;
    private bool _audioFailed;
    private bool _closing;
    private SaveIntent _saveIntent;

    public MainWindow() : this(SettingsStore.ForCurrentUser(), new TransitionSoundPlayer(), NativeWindow.ReadClock) { }

    internal MainWindow(SettingsStore store, TransitionSoundPlayer sounds, Func<ClockSample> clock,
        Func<TaskbarWidget, int, TaskbarPlacement.Result>? placeWidget = null)
    {
        _store = store;
        _sounds = sounds;
        _clock = clock;
        _placeWidget = placeWidget;
        var loaded = store.Load();
        _settings = _savedSettings = loaded.Settings;
        _needsMigration = loaded.Migrate;
        if (loaded.ReadWarning) _settingsProblem = "Ajustes protegidos tras un error de lectura.";
        InitializeComponent();

        _sounds.TransitionCompleted += TransitionSoundCompleted;
        _timer.Tick += (_, _) => UpdateTimer();
        if (_needsMigration) Persist();
        UpdateStatus();
        UpdateTimer();
        _timer.Start();
    }

    internal void ShowSavedView()
    {
        if (_settings.TaskbarMode) ShowWidget(focus: false);
        else ShowWindow();
    }

    internal bool BringToFront()
    {
        if (_closing) return false;
        CaptureGeometry();
        if (_settings.TaskbarMode)
        {
            _settings = _settings with { TaskbarMode = false };
            Persist(SaveIntent.Edit);
        }
        ShowWindow();
        Activate();
        Focus();
        return true;
    }

    internal void RecoverWindow()
    {
        if (!_closing) ShowWindow();
    }

    private void ShowWindow()
    {
        CloseWidget();
        var geometry = IsVisible ? WindowPosition.Capture(this) : _settings.Geometry;
        WindowState = WindowState.Normal;
        WindowPosition.Restore(this, geometry);
        if (!IsVisible)
        {
            Show();
            WindowPosition.Restore(this, geometry);
            _displayedGeometry = WindowPosition.Capture(this);
        }
    }

    internal void RefreshDisplay()
    {
        if (_closing) return;
        if (_widget?.RefreshPlacement() == TaskbarPlacement.Result.Unavailable)
            RecoverWindow();
        if (IsVisible)
        {
            CaptureGeometry();
            WindowPosition.Restore(this, _settings.Geometry);
            _displayedGeometry = WindowPosition.Capture(this);
        }
    }

    private void CaptureGeometry()
    {
        if (!IsVisible || WindowState != WindowState.Normal) return;
        var current = WindowPosition.Capture(this);
        if (current != _displayedGeometry && WindowPosition.IsValid(current))
        {
            _settings = _settings with { Geometry = current };
            _displayedGeometry = current;
        }
    }

    internal void ResynchronizeClock()
    {
        _transitions.Resynchronize();
        UpdateTimer();
    }

    internal void SetPowerWarning(bool failed)
    {
        _powerFailed = failed;
        UpdateStatus();
    }

    internal void UpdateTimer()
    {
        if (_closing) return;
        var (snapshot, alert) = _transitions.Update(_clock(), _settings.Mode);
        CountdownText.Text = snapshot.Countdown;
        PhaseText.Text = snapshot.Label;
        Background = snapshot.Phase == PomodoroPhase.Rest ? RestBrush
            : _settings.Mode == PomodoroMode.Standard25_5 ? WorkBrush : ExtendedWorkBrush;
        _widget?.Update(snapshot, _settings, StatusText.Text);

        if (alert) _sounds.PlayTransition(snapshot.Phase, _settings.VolumePercent);
    }

    private void TransitionSoundCompleted(PlaybackResult result)
    {
        if (Dispatcher.HasShutdownStarted) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (_closing) return;
            _audioFailed = result == PlaybackResult.Failed;
            UpdateStatus();
        });
    }

    private void UpdateStatus()
    {
        var messages = new[]
        {
            _settingsProblem,
            _audioFailed ? "Ha fallado un aviso sonoro." : "",
            _powerFailed ? "Windows no ha aceptado mantener el equipo despierto." : ""
        };
        var status = string.Join(" ", messages.Where(message => message.Length > 0));
        if (StatusText.Text == status) return;

        StatusText.Text = status;
        StatusText.ToolTip = status;
        StatusText.Visibility = status.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (status.Length > 0)
            UIElementAutomationPeer.CreatePeerForElement(StatusText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        _settingsWindow?.ShowSaveStatus(_settingsProblem);
    }

    private bool Persist(SaveIntent intent = SaveIntent.None)
    {
        if (intent > _saveIntent) _saveIntent = intent;
        if (_settings == _savedSettings && !_needsMigration && _saveIntent == SaveIntent.None) return true;

        var result = _store.Save(_settings, _saveIntent != SaveIntent.None, _saveIntent == SaveIntent.Reset);
        if (result == SettingsSaveResult.Saved)
        {
            _savedSettings = _settings;
            _saveIntent = SaveIntent.None;
            _needsMigration = false;
        }

        _settingsProblem = result switch
        {
            SettingsSaveResult.ReadProtected => "Ajustes protegidos. Restablece en Ajustes para reemplazar el archivo.",
            SettingsSaveResult.Failed => "Ajustes pendientes. Abre Ajustes y pulsa LISTO para reintentar.",
            _ => ""
        };
        UpdateStatus();
        return result == SettingsSaveResult.Saved;
    }

    private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsInteractiveSource(e.OriginalSource as DependencyObject) &&
            e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var settingsWindow = new SettingsWindow(_settings.VolumePercent, _settings.Mode, _sounds) { Owner = this };
        _settingsWindow = settingsWindow;
        settingsWindow.ShowSaveStatus(_settingsProblem);
        settingsWindow.Closed += SettingsWindow_Closed;
        settingsWindow.Show();
    }

    private void SettingsWindow_Closed(object? sender, EventArgs e)
    {
        _settingsWindow = null;
        if (sender is SettingsWindow window)
            ApplySettings(window.VolumePercent, window.SelectedMode, window.ResetRequested);
    }

    internal void ApplySettings(int volume, PomodoroMode mode, bool reset)
    {
        CaptureGeometry();
        var previous = _settings;
        if (reset) _settings = _store.Defaults() with { TaskbarMode = _settings.TaskbarMode };
        _settings = _settings.WithVolume(volume) with { Mode = mode };
        if (reset && IsVisible)
        {
            WindowPosition.Restore(this, _settings.Geometry);
            _displayedGeometry = WindowPosition.Capture(this);
        }

        if (mode != previous.Mode) _transitions.Resynchronize();
        Persist(reset ? SaveIntent.Reset : _settings != previous ? SaveIntent.Edit : SaveIntent.None);
        UpdateTimer();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) => NativeWindow.EnableRoundedCorners(this);

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _closing = true;
        _settingsWindow?.Close();
        CaptureGeometry();
        if (!Persist() && (_settings != _savedSettings || _saveIntent != SaveIntent.None) &&
            MessageBox.Show($"{_settingsProblem}\n\n¿Salir sin guardar los cambios?", "AutoPomodoro",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            _closing = false;
            if (_widget is null && !IsVisible) RecoverWindow();
            return;
        }

        _timer.Stop();
        CloseWidget();
        _sounds.TransitionCompleted -= TransitionSoundCompleted;
        _sounds.Dispose();
    }

    internal void UseTaskbar()
    {
        if (_closing || _widget is not null) return;
        _settingsWindow?.Close();
        CaptureGeometry();
        if (!_settings.TaskbarMode)
        {
            _settings = _settings with { TaskbarMode = true };
            Persist(SaveIntent.Edit);
        }
        ShowWidget(focus: true);
    }

    private void ShowWidget(bool focus)
    {
        var widget = new TaskbarWidget(this, _placeWidget);
        _widget = widget;
        widget.Closed += WidgetClosed;
        UpdateTimer();
        if (!widget.TryShow(focus))
        {
            RecoverWindow();
            return;
        }
        if (IsVisible) Hide();
    }

    private void WidgetClosed(object? sender, EventArgs e)
    {
        if (sender is not TaskbarWidget widget || widget != _widget) return;
        _widget = null;
        Dispatcher.InvokeAsync(() =>
        {
            if (_closing || _widget is not null) return;
            if (widget.ExitRequested) Close();
            else RecoverWindow();
        });
    }

    private void CloseWidget()
    {
        var widget = _widget;
        _widget = null;
        if (widget is null) return;
        widget.Closed -= WidgetClosed;
        widget.Close();
    }

    internal void ChangeVolume(int volume)
    {
        var previous = _settings;
        _settings = _settings.WithVolume(volume);
        Persist(_settings != previous ? SaveIntent.Edit : SaveIntent.None);
        UpdateTimer();
    }

    internal void ToggleMute() => ChangeVolume(_settings.VolumePercent == 0 ? _settings.LastAudibleVolume : 0);

    private void Minimize_Click(object sender, RoutedEventArgs e) => UseTaskbar();

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized || _closing) return;
        WindowState = WindowState.Normal;
        UseTaskbar();
    }

    internal static bool IsInteractiveSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.Slider or System.Windows.Controls.Primitives.Thumb)
                return true;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }

    private enum SaveIntent { None, Edit, Reset }
}
