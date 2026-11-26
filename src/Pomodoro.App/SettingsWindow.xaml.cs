using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using Pomodoro.Core;

namespace Pomodoro.App;

public partial class SettingsWindow : Window
{
    private readonly TransitionSoundPlayer _sounds;
    private bool _closed;

    internal SettingsWindow(int volumePercent, PomodoroMode mode, TransitionSoundPlayer sounds)
    {
        _sounds = sounds;
        InitializeComponent();
        VolumeSlider.Value = volumePercent;
        StandardMode.IsChecked = mode == PomodoroMode.Standard25_5;
        ExtendedMode.IsChecked = mode == PomodoroMode.Extended50_10;
        UpdateVolumeText();
        Closed += (_, _) =>
        {
            _closed = true;
            _sounds.CancelTests();
        };
    }

    public int VolumePercent => (int)Math.Round(VolumeSlider.Value);

    public PomodoroMode SelectedMode => ExtendedMode.IsChecked == true
        ? PomodoroMode.Extended50_10
        : PomodoroMode.Standard25_5;

    internal bool ResetRequested { get; private set; }

    internal void ShowSaveStatus(string message)
    {
        SaveStatus.Text = message;
        SaveStatus.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        UpdateVolumeText();

    private void UpdateVolumeText()
    {
        if (VolumePercentText is not null)
        {
            VolumePercentText.Text = $"{VolumePercent}%";
        }
    }

    private async void TestVolume_Click(object sender, RoutedEventArgs e)
    {
        var volume = VolumePercent;
        var result = await _sounds.PlayTest(volume).ConfigureAwait(false);
        if (!Dispatcher.HasShutdownStarted)
            _ = Dispatcher.InvokeAsync(() => ShowTestResult(result, volume));
    }

    private void ShowTestResult(PlaybackResult result, int volume)
    {
        if (_closed || result is PlaybackResult.Busy or PlaybackResult.Cancelled) return;
        TestStatus.Text = result switch
        {
            PlaybackResult.Played => $"Prueba reproducida al {volume} %.",
            PlaybackResult.Muted => "El volumen está al 0 %.",
            _ => "Windows no ha podido reproducir la prueba. Revisa el dispositivo de audio."
        };
        TestStatus.Visibility = Visibility.Visible;
        UIElementAutomationPeer.CreatePeerForElement(TestStatus)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        VolumeSlider.Value = SettingsStore.DefaultVolumePercent;
        StandardMode.IsChecked = true;
        ResetRequested = true;
    }

    private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!MainWindow.IsInteractiveSource(e.OriginalSource as DependencyObject) &&
            e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindow.EnableRoundedCorners(this);
}
