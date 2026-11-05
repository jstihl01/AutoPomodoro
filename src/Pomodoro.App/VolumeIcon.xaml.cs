using System.Windows;
using System.Windows.Controls;

namespace Pomodoro.App;

public partial class VolumeIcon : UserControl
{
    public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
        nameof(Volume), typeof(int), typeof(VolumeIcon), new PropertyMetadata(60, OnVolumeChanged));
    public int Volume { get => (int)GetValue(VolumeProperty); set => SetValue(VolumeProperty, value); }
    public VolumeIcon() { InitializeComponent(); UpdateWaves(); }
    private static void OnVolumeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((VolumeIcon)sender).UpdateWaves();
    private void UpdateWaves()
    {
        if (Low is null) return;
        Low.Visibility = Volume > 0 ? Visibility.Visible : Visibility.Collapsed;
        Medium.Visibility = Volume > 33 ? Visibility.Visible : Visibility.Collapsed;
        High.Visibility = Volume > 66 ? Visibility.Visible : Visibility.Collapsed;
        Muted.Visibility = Volume == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
