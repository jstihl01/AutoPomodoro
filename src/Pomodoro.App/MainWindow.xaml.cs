using System.Diagnostics;
using System.Windows;
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

    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render);
    private readonly Stopwatch _awakeClock = Stopwatch.StartNew();
    private readonly TransitionSoundPlayer _sounds = new();
    private AppSettings _settings;
    private SettingsWindow? _settingsWindow;
    private TaskbarWidget? _widget;
    private PomodoroSnapshot? _previousSnapshot;
    private DateTimeOffset? _previousWallTime;
    private TimeSpan _previousAwakeTime;

    public MainWindow()
    {
        _settings = SettingsStore.Load();
        InitializeComponent();
        ApplySavedGeometry();
        UpdateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += (_, _) => UpdateTimer();
        _timer.Start();
    }

    internal void ShowSavedView()
    {
        if (_settings.TaskbarMode)
            UseTaskbar();
        else
            Show();
    }

    public void BringToFront()
    {
        UseWindow();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Topmost = true;
        Activate();
        Focus();
    }

    private void ApplySavedGeometry()
    {
        var geometry = _settings.Geometry;
        Left = geometry.Left;
        Top = geometry.Top;
        Width = geometry.Width;
        Height = geometry.Height;
    }

    private void UpdateTimer()
    {
        var now = DateTimeOffset.Now;
        var awakeNow = _awakeClock.Elapsed;
        var snapshot = PomodoroClock.At(now, _settings.Mode);

        CountdownText.Text = snapshot.Countdown;
        PhaseText.Text = snapshot.Label;
        ApplyPhase(snapshot.Phase, _settings.Mode);
        _widget?.Update(snapshot, _settings);

        if (_previousSnapshot is { } previous && previous.Phase != snapshot.Phase &&
            _previousWallTime is { } previousWall)
        {
            var wallGap = now - previousWall;
            var awakeGap = awakeNow - _previousAwakeTime;
            if (wallGap >= TimeSpan.Zero && wallGap <= TimeSpan.FromSeconds(2) &&
                awakeGap <= TimeSpan.FromSeconds(2))
            {
                if (snapshot.Phase == PomodoroPhase.Rest)
                {
                    _sounds.PlayWorkFinished(_settings.VolumePercent);
                }
                else
                {
                    _sounds.PlayRestFinished(_settings.VolumePercent);
                }
            }
        }

        _previousSnapshot = snapshot;
        _previousWallTime = now;
        _previousAwakeTime = awakeNow;
    }

    private void ApplyPhase(PomodoroPhase phase, PomodoroMode mode)
    {
        Background = phase == PomodoroPhase.Rest ? RestBrush
            : mode == PomodoroMode.Standard25_5 ? WorkBrush : ExtendedWorkBrush;
    }

    private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsInteractiveSource(e.OriginalSource as DependencyObject) &&
            e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
        => OpenSettings();

    internal void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var settingsWindow = new SettingsWindow(
            _settings.VolumePercent,
            _settings.Mode,
            _sounds)
        {
            Owner = IsVisible ? this : _widget,
            WindowStartupLocation = IsVisible ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
        };
        _settingsWindow = settingsWindow;
        settingsWindow.ResetWindowRequested += (_, _) =>
        {
            ApplyGeometry(SettingsStore.Defaults().Geometry);
            SaveTaskbarOffset(0);
        };
        settingsWindow.Closed += SettingsWindow_Closed;
        settingsWindow.Show();
    }

    private void ApplyGeometry(WindowGeometry geometry)
    {
        Left = geometry.Left;
        Top = geometry.Top;
        Width = geometry.Width;
        Height = geometry.Height;
    }

    private void SettingsWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is SettingsWindow settingsWindow)
        {
            var modeChanged = _settings.Mode != settingsWindow.SelectedMode;
            _settings = _settings with
            {
                Geometry = SettingsStore.GeometryOf(this),
                VolumePercent = settingsWindow.VolumePercent,
                Mode = settingsWindow.SelectedMode,
                LastAudibleVolume = settingsWindow.VolumePercent > 0 ? settingsWindow.VolumePercent : _settings.LastAudibleVolume
            };
            SettingsStore.Save(_settings);

            if (modeChanged)
            {
                _previousSnapshot = null;
                _previousWallTime = null;
            }

            UpdateTimer();
        }

        _settingsWindow = null;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindow.EnableRoundedCorners(this);

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
        _widget?.Close();
        _widget = null;
        if (_settingsWindow is not null)
        {
            _settingsWindow.Close();
        }

        if (IsVisible)
            _settings = _settings with { Geometry = SettingsStore.GeometryOf(this) };
        SettingsStore.Save(_settings);
    }

    internal void UseTaskbar()
    {
        if (_widget is not null) return;
        _settingsWindow?.Close();
        if (IsVisible)
            _settings = _settings with { Geometry = SettingsStore.GeometryOf(this) };
        _settings = _settings with { TaskbarMode = true };
        SettingsStore.Save(_settings);
        var widget = new TaskbarWidget(this);
        _widget = widget;
        UpdateTimer();
        widget.Show();
        if (_widget is not null && IsVisible) Hide();
    }

    internal void UseWindow()
    {
        _settingsWindow?.Close();
        _settings = _settings with { TaskbarMode = false };
        var widget = _widget;
        _widget = null;
        widget?.Close();
        Show();
        WindowState = WindowState.Normal;
        Activate();
        SettingsStore.Save(_settings);
    }

    internal void ChangeMode(PomodoroMode mode)
    {
        _settings = _settings with { Mode = mode };
        _previousSnapshot = null;
        _previousWallTime = null;
        SettingsStore.Save(_settings);
        UpdateTimer();
    }

    internal void ChangeVolume(int volume)
    {
        _settings = _settings with
        {
            VolumePercent = Math.Clamp(volume, 0, 100),
            LastAudibleVolume = volume > 0 ? Math.Clamp(volume, 1, 100) : _settings.LastAudibleVolume
        };
        SettingsStore.Save(_settings);
        UpdateTimer();
    }

    internal void ToggleMute() => ChangeVolume(_settings.VolumePercent == 0 ? _settings.LastAudibleVolume : 0);

    internal void SaveTaskbarOffset(int offset)
    {
        _settings = _settings with { TaskbarOffset = Math.Clamp(offset, 0, 4000) };
        SettingsStore.Save(_settings);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => UseTaskbar();

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
            UseTaskbar();
        }
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
}
