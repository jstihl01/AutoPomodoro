using System.Windows;
using System.Windows.Input;
using Pomodoro.Core;

namespace Pomodoro.App;

public partial class SettingsWindow : Window
{
    private readonly TransitionSoundPlayer _sounds;

    internal SettingsWindow(int volumePercent, PomodoroMode mode, TransitionSoundPlayer sounds)
    {
        _sounds = sounds;
        InitializeComponent();
        VolumeSlider.Value = volumePercent;
        StandardMode.IsChecked = mode == PomodoroMode.Standard25_5;
        ExtendedMode.IsChecked = mode == PomodoroMode.Extended50_10;
        UpdateVolumeText();
    }

    public int VolumePercent => (int)Math.Round(VolumeSlider.Value);

    public PomodoroMode SelectedMode => ExtendedMode.IsChecked == true
        ? PomodoroMode.Extended50_10
        : PomodoroMode.Standard25_5;

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        UpdateVolumeText();

    private void UpdateVolumeText()
    {
        if (VolumePercentText is not null)
        {
            VolumePercentText.Text = $"{VolumePercent}%";
        }
    }

    private void TestVolume_Click(object sender, RoutedEventArgs e) =>
        _sounds.PlayTest(VolumePercent);

    private void Done_Click(object sender, RoutedEventArgs e) => Close();

    private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindow.EnableRoundedCorners(this);
}
