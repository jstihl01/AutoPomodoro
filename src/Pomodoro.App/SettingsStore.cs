using System.IO;
using System.Text.Json;
using System.Windows;
using Pomodoro.Core;

namespace Pomodoro.App;

internal readonly record struct WindowGeometry(double Left, double Top, double Width, double Height);

internal readonly record struct AppSettings(
    WindowGeometry Geometry,
    int VolumePercent,
    PomodoroMode Mode);

internal static class SettingsStore
{
    public const int DefaultVolumePercent = 60;
    private const double DefaultSize = 320;
    private const double Margin = 24;
    private static readonly string LocalAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string DirectoryPath = Path.Combine(
        LocalAppData, "AutoPomodoro");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");
    private static readonly string LegacyFilePath = Path.Combine(LocalAppData, "Pomodoro", "settings.json");

    public static AppSettings Defaults() =>
        new(DefaultGeometry(), DefaultVolumePercent, PomodoroMode.Standard25_5);

    public static AppSettings Load()
    {
        try
        {
            var sourcePath = File.Exists(FilePath) ? FilePath : LegacyFilePath;
            if (File.Exists(sourcePath))
            {
                var stored = JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(sourcePath));
                if (stored is not null)
                {
                    var geometry = new WindowGeometry(
                        stored.Left ?? double.NaN,
                        stored.Top ?? double.NaN,
                        stored.Width ?? double.NaN,
                        stored.Height ?? double.NaN);
                    if (!IsValid(geometry) || !IsVisible(geometry))
                    {
                        geometry = DefaultGeometry();
                    }

                    var volume = Math.Clamp(stored.VolumePercent ?? DefaultVolumePercent, 0, 100);
                    var mode = stored.Mode is PomodoroMode.Standard25_5 or PomodoroMode.Extended50_10
                        ? stored.Mode.Value
                        : PomodoroMode.Standard25_5;
                    var settings = new AppSettings(geometry, volume, mode);
                    if (sourcePath == LegacyFilePath)
                    {
                        Save(settings);
                    }
                    return settings;
                }
            }
        }
        catch (IOException) { }
        catch (JsonException) { }
        catch (UnauthorizedAccessException) { }

        return Defaults();
    }

    public static void Save(AppSettings settings)
    {
        if (!IsValid(settings.Geometry))
        {
            return;
        }

        var stored = new StoredSettings
        {
            Left = settings.Geometry.Left,
            Top = settings.Geometry.Top,
            Width = settings.Geometry.Width,
            Height = settings.Geometry.Height,
            VolumePercent = Math.Clamp(settings.VolumePercent, 0, 100),
            Mode = settings.Mode
        };

        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(stored));
            File.Move(temporaryPath, FilePath, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public static WindowGeometry GeometryOf(Window window)
    {
        var bounds = window.RestoreBounds;
        return new WindowGeometry(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }

    private static WindowGeometry DefaultGeometry()
    {
        var area = SystemParameters.WorkArea;
        return new WindowGeometry(
            area.Right - DefaultSize - Margin,
            area.Top + Margin,
            DefaultSize,
            DefaultSize);
    }

    private static bool IsValid(WindowGeometry value) =>
        double.IsFinite(value.Left) && double.IsFinite(value.Top) &&
        double.IsFinite(value.Width) && double.IsFinite(value.Height) &&
        value.Width >= 220 && value.Height >= 180 &&
        value.Width <= 4000 && value.Height <= 4000;

    private static bool IsVisible(WindowGeometry value)
    {
        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        var window = new Rect(value.Left, value.Top, value.Width, value.Height);
        window.Intersect(virtualScreen);
        return window.Width >= 80 && window.Height >= 80;
    }

    private sealed class StoredSettings
    {
        public double? Left { get; set; }
        public double? Top { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
        public int? VolumePercent { get; set; }
        public PomodoroMode? Mode { get; set; }
    }
}
