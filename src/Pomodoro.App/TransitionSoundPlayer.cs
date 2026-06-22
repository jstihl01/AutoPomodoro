using System.IO;
using System.Runtime.InteropServices;

namespace Pomodoro.App;

internal sealed class TransitionSoundPlayer
{
    private const uint SoundMemory = 0x0004;
    private const uint SoundNodefault = 0x0002;
    private const uint SoundSync = 0x0000;
    private readonly SemaphoreSlim _playback = new(1, 1);

    public void PlayWorkFinished(int volumePercent) =>
        QueuePlayback(volumePercent, () => Play(CreateTone(523.25, 700, Volume(volumePercent, 2.70))));

    public void PlayRestFinished(int volumePercent) => QueuePlayback(volumePercent, () =>
    {
        var tone = CreateTone(659.25, 170, Volume(volumePercent, 2.25));
        for (var i = 0; i < 3; i++)
        {
            Play(tone);
            if (i < 2)
            {
                Thread.Sleep(130);
            }
        }
    });

    public void PlayTest(int volumePercent) =>
        QueuePlayback(volumePercent, () => Play(CreateTone(659.25, 170, Volume(volumePercent, 2.25))));

    private void QueuePlayback(int volumePercent, Action playback)
    {
        if (volumePercent <= 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            await _playback.WaitAsync();
            try
            {
                playback();
            }
            finally
            {
                _playback.Release();
            }
        });
    }

    private static double Volume(int volumePercent, double maximum) =>
        Math.Clamp(volumePercent, 0, 100) / 100d * maximum;

    private static void Play(byte[] wave)
    {
        var handle = GCHandle.Alloc(wave, GCHandleType.Pinned);
        try
        {
            PlaySound(handle.AddrOfPinnedObject(), IntPtr.Zero, SoundMemory | SoundNodefault | SoundSync);
        }
        finally
        {
            handle.Free();
        }
    }

    private static byte[] CreateTone(double frequency, int durationMilliseconds, double volume)
    {
        const int sampleRate = 44_100;
        const short channels = 1;
        const short bitsPerSample = 16;
        var sampleCount = sampleRate * durationMilliseconds / 1000;
        var dataLength = sampleCount * sizeof(short);

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
        for (var sample = 0; sample < sampleCount; sample++)
        {
            var envelope = 1d;
            if (sample < fadeSamples)
            {
                envelope = sample / (double)fadeSamples;
            }
            else if (sample >= sampleCount - fadeSamples)
            {
                envelope = (sampleCount - sample - 1) / (double)fadeSamples;
            }

            var value = Math.Sin(2 * Math.PI * frequency * sample / sampleRate);
            var limitedValue = Math.Tanh(volume * value) * 0.98;
            writer.Write((short)(short.MaxValue * envelope * limitedValue));
        }

        return stream.ToArray();
    }

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(IntPtr pszSound, IntPtr hmod, uint fdwSound);
}
