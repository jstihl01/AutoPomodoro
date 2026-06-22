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
    private static readonly MediaColor WorkStart = MediaColor.FromRgb(99, 180, 71);
    private static readonly MediaColor WorkEnd = MediaColor.FromRgb(22, 128, 77);
    private static readonly MediaColor ExtendedWorkStart = MediaColor.FromRgb(224, 72, 72);
    private static readonly MediaColor ExtendedWorkEnd = MediaColor.FromRgb(161, 30, 48);
    private static readonly MediaColor RestStart = MediaColor.FromRgb(48, 131, 246);
    private static readonly MediaColor RestEnd = MediaColor.FromRgb(5, 91, 202);

    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render);
    private readonly Stopwatch _awakeClock = Stopwatch.StartNew();
    private readonly TransitionSoundPlayer _sounds = new();
    private AppSettings _settings;
    private SettingsWindow? _settingsWindow;
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

    public void BringToFront()
    {
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
        if (phase == PomodoroPhase.Work)
        {
            var start = mode == PomodoroMode.Standard25_5 ? WorkStart : ExtendedWorkStart;
            var end = mode == PomodoroMode.Standard25_5 ? WorkEnd : ExtendedWorkEnd;
            ColorStart.Color = start;
            ColorEnd.Color = end;
            Background = new SolidColorBrush(end);
        }
        else
        {
            ColorStart.Color = RestStart;
            ColorEnd.Color = RestEnd;
            Background = new SolidColorBrush(RestEnd);
        }
    }

    private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
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
            Owner = this
        };
        _settingsWindow = settingsWindow;
        settingsWindow.ResetWindowRequested += (_, _) =>
            ApplyGeometry(SettingsStore.Defaults().Geometry);
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
            _settings = new AppSettings(
                SettingsStore.GeometryOf(this),
                settingsWindow.VolumePercent,
                settingsWindow.SelectedMode);
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
        if (_settingsWindow is not null)
        {
            _settingsWindow.Close();
        }

        _settings = _settings with { Geometry = SettingsStore.GeometryOf(this) };
        SettingsStore.Save(_settings);
    }
}
