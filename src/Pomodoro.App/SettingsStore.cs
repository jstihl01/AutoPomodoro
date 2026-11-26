using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Pomodoro.Core;

namespace Pomodoro.App;

internal readonly record struct WindowGeometry(double Left, double Top, double Width, double Height, double? PositionScale = null);

internal readonly record struct AppSettings(
    WindowGeometry Geometry,
    int VolumePercent,
    PomodoroMode Mode,
    bool TaskbarMode = false,
    int LastAudibleVolume = SettingsStore.DefaultVolumePercent,
    int TaskbarOffset = 0)
{
    public AppSettings WithVolume(int percent)
    {
        var volume = Math.Clamp(percent, 0, 100);
        return this with { VolumePercent = volume, LastAudibleVolume = volume > 0 ? volume : LastAudibleVolume };
    }
}

internal enum SettingsSaveResult { Saved, ReadProtected, Failed }

internal sealed class SettingsStore(string profileDirectory, Func<WindowGeometry> defaultGeometry)
{
    public const int DefaultVolumePercent = 60;
    internal const int MaximumFileBytes = 64 * 1024;

    private readonly string _path = Path.Combine(profileDirectory, "AutoPomodoro", "settings.json");
    private readonly string _legacy = Path.Combine(profileDirectory, "Pomodoro", "settings.json");
    private bool _readProtected;
    private bool _recovered;

    public static SettingsStore ForCurrentUser() =>
        new(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), WindowPosition.Default);

    public AppSettings Defaults() => new(defaultGeometry(), DefaultVolumePercent, PomodoroMode.Standard25_5);

    public (AppSettings Settings, bool Migrate, bool ReadWarning) Load()
    {
        _recovered = false;
        var current = Read(_path);
        _readProtected = current.Failed;
        if (current.Settings is { } saved)
            return (saved, false, false);

        var backup = Read(_path + ".bak");
        var legacy = backup.Settings is null ? Read(_legacy) : default;
        var fallback = backup.Settings ?? legacy.Settings;
        _recovered = fallback.HasValue;
        _readProtected |= fallback is null && (backup.Failed || legacy.Failed);
        return (fallback ?? Defaults(), _recovered && !_readProtected, _readProtected);
    }

    public SettingsSaveResult Save(AppSettings settings, bool explicitChange = false, bool reset = false)
    {
        if (_readProtected && !reset && !(_recovered && explicitChange))
            return SettingsSaveResult.ReadProtected;

        var id = Guid.NewGuid().ToString("N");
        var temporary = $"{_path}.{id}.tmp";
        var ownsTemporary = false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsTemporary = true;
                JsonSerializer.Serialize(stream, Store(settings));
                stream.Flush(flushToDisk: true);
            }

            // Keep an unreadable original separately so it never replaces a valid backup.
            var backup = _readProtected ? $"{_path}.{id}.recovered" : _path + ".bak";
            try
            {
                File.Replace(temporary, _path, backup);
            }
            catch (FileNotFoundException)
            {
                File.Move(temporary, _path);
            }
            ownsTemporary = false;
            _readProtected = false;
            return SettingsSaveResult.Saved;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("AutoPomodoro: guardar ajustes ({0}, 0x{1:X8})", error.GetType().Name, error.HResult);
            return SettingsSaveResult.Failed;
        }
        finally
        {
            if (ownsTemporary)
            {
                try { File.Delete(temporary); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    Trace.TraceWarning("AutoPomodoro: limpiar temporal de ajustes (0x{0:X8})", error.HResult);
                }
            }
        }
    }

    private (AppSettings? Settings, bool Failed) Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumFileBytes) throw new InvalidDataException("Archivo de ajustes demasiado grande.");
            var buffer = new byte[MaximumFileBytes + 1];
            var size = stream.ReadAtLeast(buffer, buffer.Length, false);
            if (size > MaximumFileBytes) throw new InvalidDataException("Archivo de ajustes demasiado grande.");
            var json = buffer.AsSpan(0, size);
            if (json.StartsWith("\uFEFF"u8)) json = json[3..];
            var stored = JsonSerializer.Deserialize<StoredSettings>(json);
            if (stored is null || (stored.Width is null && stored.Height is null && stored.VolumePercent is null && stored.Mode is null))
                throw new InvalidDataException("Formato de ajustes desconocido.");

            var geometry = new WindowGeometry(
                stored.Left ?? double.NaN, stored.Top ?? double.NaN,
                stored.Width ?? double.NaN, stored.Height ?? double.NaN, stored.PositionScale);
            var volume = Math.Clamp(stored.VolumePercent ?? DefaultVolumePercent, 0, 100);
            var mode = stored.Mode is PomodoroMode.Extended50_10 ? PomodoroMode.Extended50_10 : PomodoroMode.Standard25_5;
            var lastAudible = volume > 0 ? volume : Math.Clamp(stored.LastAudibleVolume ?? DefaultVolumePercent, 1, 100);
            return (new AppSettings(Normalize(geometry), volume, mode, stored.TaskbarMode,
                lastAudible, Math.Clamp(stored.TaskbarOffset, 0, 4000)), false);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return (null, false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            Trace.TraceWarning("AutoPomodoro: leer ajustes{0} ({1}, 0x{2:X8})",
                path == _legacy ? " heredados" : path == _path ? "" : " de respaldo", error.GetType().Name, error.HResult);
            return (null, true);
        }
    }

    private WindowGeometry Normalize(WindowGeometry geometry) =>
        WindowPosition.IsValid(geometry) ? geometry : defaultGeometry();

    private StoredSettings Store(AppSettings settings)
    {
        var geometry = Normalize(settings.Geometry);
        return new StoredSettings
        {
            Left = geometry.Left, Top = geometry.Top, Width = geometry.Width, Height = geometry.Height,
            PositionScale = geometry.PositionScale, VolumePercent = settings.VolumePercent,
            Mode = settings.Mode, TaskbarMode = settings.TaskbarMode, LastAudibleVolume = settings.LastAudibleVolume,
            TaskbarOffset = settings.TaskbarOffset
        };
    }

    private sealed class StoredSettings
    {
        public double? Left { get; set; }
        public double? Top { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
        public double? PositionScale { get; set; }
        public int? VolumePercent { get; set; }
        public PomodoroMode? Mode { get; set; }
        public bool TaskbarMode { get; set; }
        public int? LastAudibleVolume { get; set; }
        public int TaskbarOffset { get; set; }
    }
}
