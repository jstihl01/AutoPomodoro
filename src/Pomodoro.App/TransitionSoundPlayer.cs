using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Pomodoro.Core;

namespace Pomodoro.App;

internal enum SoundKind { WorkFinished, RestFinished, Test }
internal enum PlaybackResult { Played, Muted, Busy, Cancelled, Failed }

internal sealed class TransitionSoundPlayer(Func<SoundKind, int, bool>? playback = null) : IDisposable
{
    private const uint SoundMemory = 0x0004;
    private const uint SoundNodefault = 0x0002;
    private const uint SoundSync = 0x0000;
    private readonly object _gate = new();
    private Request? _alert;
    private Request? _preview;
    private bool _playingPreview;
    private bool _running;
    private bool _disposed;

    public event Action<PlaybackResult>? TransitionCompleted;

    public void PlayTransition(PomodoroPhase newPhase, int volume)
    {
        if (volume <= 0) return;
        lock (_gate)
        {
            if (_disposed) return;
            _alert = new Request(newPhase == PomodoroPhase.Rest ? SoundKind.WorkFinished : SoundKind.RestFinished, volume);
            StartWorker();
        }
    }

    public Task<PlaybackResult> PlayTest(int volume)
    {
        if (volume <= 0) return Task.FromResult(PlaybackResult.Muted);
        lock (_gate)
        {
            if (_disposed) return Task.FromResult(PlaybackResult.Cancelled);
            if (_playingPreview || _preview is not null) return Task.FromResult(PlaybackResult.Busy);
            var completion = new TaskCompletionSource<PlaybackResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _preview = new Request(SoundKind.Test, volume, completion);
            StartWorker();
            return completion.Task;
        }
    }

    public void CancelTests()
    {
        lock (_gate)
        {
            _preview?.Completion?.TrySetResult(PlaybackResult.Cancelled);
            _preview = null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _alert = null;
            CancelTests();
        }
    }

    private void StartWorker()
    {
        if (_running) return;
        _running = true;
        _ = Task.Run(ProcessQueue);
    }

    private void ProcessQueue()
    {
        while (true)
        {
            Request request;
            lock (_gate)
            {
                if ((_alert ?? _preview) is not { } next)
                {
                    _running = false;
                    return;
                }
                request = next;
                _playingPreview = next.Kind == SoundKind.Test;
                if (_playingPreview) _preview = null;
                else _alert = null;
            }

            var result = PlaybackResult.Failed;
            try
            {
                if ((playback ?? PlayNative)(request.Kind, request.Volume))
                    result = PlaybackResult.Played;
                else
                    Trace.TraceWarning("AutoPomodoro: Windows rechazo el sonido {0}.", request.Kind);
            }
            catch (Exception error)
            {
                Trace.TraceWarning("AutoPomodoro: reproducir {0} ({1}, 0x{2:X8})", request.Kind, error.GetType().Name, error.HResult);
            }

            lock (_gate) _playingPreview = false;
            if (request.Completion is { } completion)
                completion.TrySetResult(result);
            else
                TransitionCompleted?.Invoke(result);
        }
    }

    private readonly record struct Request(SoundKind Kind, int Volume, TaskCompletionSource<PlaybackResult>? Completion = null);

    private static bool PlayNative(SoundKind kind, int volume) => kind switch
    {
        SoundKind.WorkFinished => Play(CreateTone(523.25, 700, Volume(volume, 8.10))),
        SoundKind.RestFinished => Play(CreateTone(659.25, 170, Volume(volume, 6.75), 3)),
        _ => Play(CreateTone(659.25, 170, Volume(volume, 6.75)))
    };

    private static double Volume(int volumePercent, double maximum) =>
        Math.Clamp(volumePercent, 0, 100) / 100d * maximum;

    private static bool Play(byte[] wave)
    {
        var handle = GCHandle.Alloc(wave, GCHandleType.Pinned);
        try
        {
            return PlaySound(handle.AddrOfPinnedObject(), IntPtr.Zero, SoundMemory | SoundNodefault | SoundSync);
        }
        finally
        {
            handle.Free();
        }
    }

    private static byte[] CreateTone(double frequency, int durationMilliseconds, double volume, int repetitions = 1)
    {
        const int sampleRate = 44_100;
        const short channels = 1;
        const short bitsPerSample = 16;
        var sampleCount = sampleRate * durationMilliseconds / 1000;
        const int gapSamples = sampleRate * 130 / 1000;
        var dataLength = (repetitions * sampleCount + (repetitions - 1) * gapSamples) * sizeof(short);

        using var stream = new MemoryStream(44 + dataLength);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataLength);
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write(bitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(dataLength);

        var fadeSamples = Math.Min(sampleRate / 25, sampleCount / 3);
        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            if (repetition > 0) writer.Write(new byte[gapSamples * sizeof(short)]);
            for (var sample = 0; sample < sampleCount; sample++)
            {
                var fade = Math.Min(sample, sampleCount - sample - 1);
                var envelope = Math.Min(1d, fade / (double)fadeSamples);
                var value = Math.Sin(2 * Math.PI * frequency * sample / sampleRate);
                var limitedValue = Math.Tanh(volume * value) * 0.98;
                writer.Write((short)(short.MaxValue * envelope * limitedValue));
            }
        }

        return stream.ToArray();
    }

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(IntPtr pszSound, IntPtr hmod, uint fdwSound);
}
