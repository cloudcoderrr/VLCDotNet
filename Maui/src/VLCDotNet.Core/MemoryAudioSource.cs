using System;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    /// <summary>
    /// Captures decoded PCM audio from libvlc through the decode-to-memory (amem)
    /// callbacks and exposes a running RMS level. This bypasses the system audio
    /// device, so it verifies the audio pipeline reliably in headless/CI runs.
    /// </summary>
    /// <remarks>
    /// Create via <see cref="MediaPlayer.EnableAudioCapture"/>; the owning player
    /// disposes it after playback is stopped.
    /// </remarks>
    public sealed class MemoryAudioSource : IDisposable
    {
        private readonly uint _channels;
        private readonly object _sync = new object();

        // Kept alive for the player's lifetime so the native pointer never dangles.
        private readonly VlcAudioPlayCb _playCb;

        private double _sumSquares;
        private long _sampleValues;
        private bool _disposed;

        internal MemoryAudioSource(MediaPlayer player, uint sampleRate, uint channels)
        {
            _channels = Math.Max(1, channels);
            _playCb = OnPlay;

            player.SetAudioFormat("S16N", sampleRate, _channels);
            player.SetAudioCallbacks(_playCb);
        }

        /// <summary>Number of individual 16-bit samples captured so far.</summary>
        public long SampleValues
        {
            get
            {
                lock (_sync)
                {
                    return _sampleValues;
                }
            }
        }

        /// <summary>Root-mean-square amplitude of the captured audio (0 = silence).</summary>
        public double Rms
        {
            get
            {
                lock (_sync)
                {
                    return _sampleValues == 0 ? 0 : Math.Sqrt(_sumSquares / _sampleValues);
                }
            }
        }

        private void OnPlay(IntPtr data, IntPtr samples, uint count, long pts)
        {
            try
            {
                int total = checked((int)(count * _channels));
                if (total <= 0 || samples == IntPtr.Zero)
                {
                    return;
                }

                var buffer = new short[total];
                Marshal.Copy(samples, buffer, 0, total);

                double sumSq = 0;
                for (int i = 0; i < total; i++)
                {
                    sumSq += (double)buffer[i] * buffer[i];
                }

                lock (_sync)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _sumSquares += sumSq;
                    _sampleValues += total;
                }
            }
            catch
            {
                // Never let a managed exception cross back into native code.
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_sync)
            {
                _disposed = true;
            }
        }
    }
}
