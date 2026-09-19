using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace VLCDotNet.Tests.Shared
{
    /// <summary>
    /// Captures decoded PCM through libvlc's decode-to-memory audio callbacks
    /// (format S16N) so audio can be verified headlessly. Computes RMS energy to
    /// prove the stream is not silent and keeps a short snippet for a WAV artifact.
    /// </summary>
    public sealed class AudioProbe : IDisposable
    {
        private readonly VlcAudioPlayCb _playCb;
        private readonly object _sync = new object();
        private readonly List<short> _snippet = new List<short>();
        private readonly int _snippetLimit;

        private long _sumSquares;
        private long _sampleValues;

        public AudioProbe(IntPtr player, uint rate = 44100, uint channels = 2, int snippetSeconds = 1)
        {
            Rate = rate;
            Channels = channels;
            _snippetLimit = (int)(rate * channels * snippetSeconds);
            _playCb = OnPlay;

            LibVlc.libvlc_audio_set_callbacks(player, _playCb, null, null, null, null, IntPtr.Zero);
            LibVlc.libvlc_audio_set_format(player, "S16N", rate, channels);
        }

        public uint Rate { get; }

        public uint Channels { get; }

        /// <summary>Root-mean-square amplitude across all captured 16-bit samples.</summary>
        public double Rms
        {
            get
            {
                lock (_sync)
                {
                    return _sampleValues == 0 ? 0 : Math.Sqrt((double)_sumSquares / _sampleValues);
                }
            }
        }

        public long SampleValues
        {
            get { lock (_sync) { return _sampleValues; } }
        }

        private void OnPlay(IntPtr data, IntPtr samples, uint count, long pts)
        {
            int shorts = checked((int)(count * Channels));
            if (shorts <= 0)
            {
                return;
            }

            var buffer = new short[shorts];
            Marshal.Copy(samples, buffer, 0, shorts);

            long ss = 0;
            for (int i = 0; i < shorts; i++)
            {
                int v = buffer[i];
                ss += (long)v * v;
            }

            lock (_sync)
            {
                _sumSquares += ss;
                _sampleValues += shorts;
                if (_snippet.Count < _snippetLimit)
                {
                    int take = Math.Min(shorts, _snippetLimit - _snippet.Count);
                    for (int i = 0; i < take; i++)
                    {
                        _snippet.Add(buffer[i]);
                    }
                }
            }
        }

        /// <summary>Writes the captured snippet to a 16-bit PCM WAV artifact.</summary>
        public bool WriteWav(string path)
        {
            short[] data;
            lock (_sync)
            {
                if (_snippet.Count == 0)
                {
                    return false;
                }
                data = _snippet.ToArray();
            }

            int byteCount = data.Length * 2;
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var w = new BinaryWriter(fs);
            w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            w.Write(36 + byteCount);
            w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            w.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            w.Write(16);
            w.Write((short)1);                         // PCM
            w.Write((short)Channels);
            w.Write((int)Rate);
            w.Write((int)(Rate * Channels * 2));        // byte rate
            w.Write((short)(Channels * 2));             // block align
            w.Write((short)16);                         // bits per sample
            w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            w.Write(byteCount);
            foreach (short s in data)
            {
                w.Write(s);
            }
            return true;
        }

        public void Dispose()
        {
        }
    }
}
