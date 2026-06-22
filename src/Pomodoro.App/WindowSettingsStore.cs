using System.IO;
using System.Text.Json;
using System.Windows;

namespace Pomodoro.App;

internal readonly record struct WindowGeometry(double Left, double Top, double Width, double Height);

internal static class WindowSettingsStore
{
    private const double DefaultSize = 320;
    private const double Margin = 24;
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pomodoro");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

    public static WindowGeometry Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var value = JsonSerializer.Deserialize<WindowGeometry>(File.ReadAllText(FilePath));
                if (IsValid(value) && IsVisible(value))
                {
                    return value;
                }
            }
        }
        catch (IOException) { }
        catch (JsonException) { }
        catch (UnauthorizedAccessException) { }

        return DefaultGeometry();
    }

    public static void Save(Window window)
    {
        var bounds = window.RestoreBounds;
        var value = new WindowGeometry(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        if (!IsValid(value))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value));
            File.Move(temporaryPath, FilePath, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
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
}
