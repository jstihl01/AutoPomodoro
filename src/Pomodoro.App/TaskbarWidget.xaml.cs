using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Pomodoro.Core;

namespace Pomodoro.App;

public partial class TaskbarWidget : Window
{
    private static readonly Brush WorkColor = new SolidColorBrush(Color.FromRgb(83, 210, 110));
    private static readonly Brush ExtendedColor = new SolidColorBrush(Color.FromRgb(255, 95, 105));
    private static readonly Brush RestColor = new SolidColorBrush(Color.FromRgb(75, 160, 255));
    private readonly MainWindow _controller;
    private readonly Func<TaskbarWidget, int, TaskbarPlacement.Result>? _placementOverride;
    private readonly DispatcherTimer _placementTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _popupTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private bool _updating;
    private bool _keyboardVolume;
    private int _offset;
    private nint _lastBar;

    internal TaskbarWidget(MainWindow controller, Func<TaskbarWidget, int, TaskbarPlacement.Result>? placementOverride)
    {
        _controller = controller;
        _placementOverride = placementOverride;
        InitializeComponent();
        VolumeButton.MouseLeave += ScheduleVolumeClose;
        VolumeSlider.PreviewKeyDown += WidgetKeyDown;
        PreviewKeyDown += WidgetKeyDown;
        VolumePopup.Closed += (_, _) => _keyboardVolume = false;
        _popupTimer.Tick += (_, _) =>
        {
            if (_keyboardVolume || VolumePanel.IsMouseOver || VolumeSlider.IsMouseCaptureWithin) return;
            VolumePopup.IsOpen = false;
            _popupTimer.Stop();
        };
        _placementTimer.Tick += (_, _) =>
        {
            if (RefreshPlacement() == TaskbarPlacement.Result.Unavailable)
                _controller.RecoverWindow();
        };
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowMessage);
        Closed += (_, _) => { _placementTimer.Stop(); _popupTimer.Stop(); VolumePopup.IsOpen = false; };
    }

    internal bool ExitRequested { get; private set; }

    internal bool TryShow(bool focus)
    {
        Opacity = 0;
        Show();
        var result = RefreshPlacement();
        if (result == TaskbarPlacement.Result.Unavailable) return false;
        Opacity = 1;
        _placementTimer.Start();
        if (focus && result == TaskbarPlacement.Result.Visible)
        {
            Activate();
            VolumeButton.Focus();
        }
        return true;
    }

    internal void Update(PomodoroSnapshot snapshot, AppSettings settings, string status)
    {
        var rest = snapshot.Phase == PomodoroPhase.Rest;
        var standard = settings.Mode == PomodoroMode.Standard25_5;
        var label = rest ? "DESCANSO" : standard ? "TRABAJO 25/5" : "TRABAJO 50/10";
        var countdown = snapshot.Countdown;
        if (Countdown.Text != countdown || Phase.Text != label)
        {
            Countdown.Text = countdown;
            Phase.Text = label;
            AutomationProperties.SetItemStatus(OpenWindowButton, $"{label}, {countdown} restantes");
        }
        Countdown.Foreground = rest ? RestColor : standard ? WorkColor : ExtendedColor;
        _offset = settings.TaskbarOffset;
        _updating = true;
        VolumeSlider.Value = settings.VolumePercent;
        VolumeLabel.Text = $"{settings.VolumePercent}%";
        Speaker.Volume = settings.VolumePercent;
        VolumeButton.IsChecked = settings.VolumePercent == 0;
        _updating = false;

        if (AutomationProperties.GetName(StatusBadge) != status)
        {
            StatusBadge.ToolTip = status;
            StatusBadge.Visibility = status.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            AutomationProperties.SetName(StatusBadge, status);
            if (status.Length > 0)
                UIElementAutomationPeer.CreatePeerForElement(StatusBadge)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }

    internal TaskbarPlacement.Result RefreshPlacement()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var result = _placementOverride?.Invoke(this, _offset) ?? TaskbarPlacement.Place(handle, _offset, ref _lastBar);
        if (result != TaskbarPlacement.Result.Visible) VolumePopup.IsOpen = false;
        return result;
    }

    private nint WindowMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        const int systemCommand = 0x0112;
        const int close = 0xF060;
        if (message == systemCommand && (wParam.ToInt64() & 0xFFF0) == close)
            ExitRequested = true;
        return 0;
    }

    private void WidgetKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2 || (e.Key == Key.Down && VolumeButton.IsKeyboardFocused))
        {
            e.Handled = true;
            _keyboardVolume = true;
            _popupTimer.Stop();
            VolumePopup.StaysOpen = false;
            VolumePopup.IsOpen = true;
            Dispatcher.InvokeAsync(() => { if (VolumePopup.IsOpen) VolumeSlider.Focus(); }, DispatcherPriority.Input);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (VolumePopup.IsOpen)
            {
                VolumePopup.IsOpen = false;
                VolumeButton.Focus();
            }
            else
            {
                _controller.BringToFront();
            }
        }
    }

    private void OpenWindow(object sender, RoutedEventArgs e) => _controller.BringToFront();

    private void MuteChanged(object sender, RoutedEventArgs e)
    {
        if (!_updating && IsLoaded) _controller.ToggleMute();
    }
    private void ShowVolume(object sender, MouseEventArgs e)
    {
        _popupTimer.Stop();
        if (_keyboardVolume) return;
        VolumePopup.StaysOpen = true;
        VolumePopup.IsOpen = true;
    }
    private void KeepVolumeOpen(object sender, MouseEventArgs e) => _popupTimer.Stop();
    private void ScheduleVolumeClose(object sender, MouseEventArgs e)
    {
        if (!_keyboardVolume) _popupTimer.Start();
    }
    private void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_updating && IsLoaded) _controller.ChangeVolume((int)Math.Round(e.NewValue));
    }
}
