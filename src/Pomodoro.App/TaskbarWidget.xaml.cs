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
    private readonly MainWindow _controller;
    private readonly DispatcherTimer _placementTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _popupTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private bool _updating;
    private int _offset;

    internal TaskbarWidget(MainWindow controller)
    {
        _controller = controller;
        InitializeComponent();
        VolumeButton.MouseLeave += ScheduleVolumeClose;
        _popupTimer.Tick += (_, _) =>
        {
            if (VolumePanel.IsMouseOver || VolumeSlider.IsMouseCaptureWithin) return;
            VolumePopup.IsOpen = false;
            _popupTimer.Stop();
        };
        _placementTimer.Tick += (_, _) => Place();
        Loaded += (_, _) => { Place(); _placementTimer.Start(); };
        Closed += (_, _) => { _placementTimer.Stop(); _popupTimer.Stop(); VolumePopup.IsOpen = false; };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) _controller.UseWindow(); };
    }

    internal void Update(PomodoroSnapshot snapshot, AppSettings settings)
    {
        Countdown.Text = snapshot.Countdown;
        _offset = settings.TaskbarOffset;
        Countdown.Foreground = new SolidColorBrush(snapshot.Phase == PomodoroPhase.Rest
            ? Color.FromRgb(75, 160, 255)
            : settings.Mode == PomodoroMode.Standard25_5
                ? Color.FromRgb(83, 210, 110) : Color.FromRgb(255, 95, 105));
        Countdown.ToolTip = $"{snapshot.Label} · {(settings.Mode == PomodoroMode.Standard25_5 ? "25/5" : "50/10")} · Clic derecho para opciones";
        _updating = true;
        VolumeSlider.Value = settings.VolumePercent;
        VolumeLabel.Text = $"Volumen: {settings.VolumePercent}%";
        VolumeButton.Content = settings.VolumePercent == 0 ? "\uE74F" : "\uE767";
        _updating = false;
    }

    private void Place()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var result = TaskbarPlacement.Place(handle, _offset);
        if (result == TaskbarPlacement.Result.Unavailable)
        {
            _controller.UseWindow();
            return;
        }
        if (result == TaskbarPlacement.Result.Hidden) VolumePopup.IsOpen = false;
    }

    private void OpenMenu(object sender, MouseButtonEventArgs e)
    {
        VolumePopup.IsOpen = false;
        var menu = new ContextMenu();
        Add("Mostrar como ventana", _controller.UseWindow);
        Add("Trabajo 25 / 5", () => _controller.ChangeMode(PomodoroMode.Standard25_5));
        Add("Trabajo 50 / 10", () => _controller.ChangeMode(PomodoroMode.Extended50_10));
        Add("Mover a la izquierda", () => { _offset = Math.Min(4000, _offset + 24); _controller.SaveTaskbarOffset(_offset); Place(); });
        Add("Mover a la derecha", () => { _offset = Math.Max(0, _offset - 24); _controller.SaveTaskbarOffset(_offset); Place(); });
        Add("Ajustes", _controller.OpenSettings);
        Add("Salir", _controller.Close);
        menu.PlacementTarget = this;
        menu.IsOpen = true;
        e.Handled = true;
        void Add(string label, Action action)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
    }

    private void ToggleMute(object sender, RoutedEventArgs e) => _controller.ToggleMute();
    private void ShowVolume(object sender, MouseEventArgs e) { _popupTimer.Stop(); VolumePopup.IsOpen = true; }
    private void KeepVolumeOpen(object sender, MouseEventArgs e) => _popupTimer.Stop();
    private void ScheduleVolumeClose(object sender, MouseEventArgs e) => _popupTimer.Start();
    private void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_updating && IsLoaded) _controller.ChangeVolume((int)Math.Round(e.NewValue));
    }
}
