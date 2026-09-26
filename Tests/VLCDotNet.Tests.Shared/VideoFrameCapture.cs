using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VLCDotNet.Tests.Shared
{
    /// <summary>Summary statistics computed over a captured video frame.</summary>
    public readonly struct FrameStats
    {
        public FrameStats(double nonDarkFraction, byte avgR, byte avgG, byte avgB, int lumaRange, int avgChannelSpread, long frames)
        {
            NonDarkFraction = nonDarkFraction;
            AvgR = avgR;
            AvgG = avgG;
            AvgB = avgB;
            LumaRange = lumaRange;
            AvgChannelSpread = avgChannelSpread;
            Frames = frames;
        }

        /// <summary>Fraction (0-1) of pixels brighter than a small luma threshold.</summary>
        public double NonDarkFraction { get; }

        public byte AvgR { get; }

        public byte AvgG { get; }

        public byte AvgB { get; }

        /// <summary>Difference between the darkest and brightest pixel luma in the frame.</summary>
        public int LumaRange { get; }

        /// <summary>Difference between the highest and lowest average RGB channel.</summary>
        public int AvgChannelSpread { get; }

        public long Frames { get; }

        public override string ToString() =>
            $"frames={Frames}, non-dark={NonDarkFraction:P1}, luma-range={LumaRange}, avg-channel-spread={AvgChannelSpread}, avgRGB=({AvgR},{AvgG},{AvgB})";
    }

    /// <summary>
    /// Captures decoded frames headlessly through libvlc's decode-to-memory
    /// (vmem) callbacks, so video output can be verified without a display. The
    /// requested chroma is RV32 (32-bit, byte order B,G,R,X on little-endian).
    /// </summary>
    public sealed class VideoFrameCapture : IDisposable
    {
        private readonly IntPtr _buffer;
        private readonly int _bufferSize;
        private readonly VlcVideoLockCb _lockCb;
        private readonly VlcVideoUnlockCb _unlockCb;
        private readonly VlcVideoDisplayCb _displayCb;
        private readonly object _sync = new object();

        private byte[]? _latest;
        private long _frames;
        private bool _disposed;

        public VideoFrameCapture(IntPtr player, int width = 320, int height = 180)
        {
            Width = width;
            Height = height;
            _bufferSize = width * height * 4;
            _buffer = Marshal.AllocHGlobal(_bufferSize);

            _lockCb = OnLock;
            _unlockCb = OnUnlock;
            _displayCb = OnDisplay;

            LibVlc.libvlc_video_set_format(player, "RV32", (uint)width, (uint)height, (uint)(width * 4));
            LibVlc.libvlc_video_set_callbacks(player, _lockCb, _unlockCb, _displayCb, IntPtr.Zero);
        }

        public int Width { get; }

        public int Height { get; }

        public long FrameCount
        {
            get { lock (_sync) { return _frames; } }
        }

        private IntPtr OnLock(IntPtr opaque, IntPtr planes)
        {
            Marshal.WriteIntPtr(planes, _buffer);
            return _buffer;
        }

        private void OnUnlock(IntPtr opaque, IntPtr picture, IntPtr planes)
        {
        }

        private void OnDisplay(IntPtr opaque, IntPtr picture)
        {
            var copy = new byte[_bufferSize];
            Marshal.Copy(_buffer, copy, 0, _bufferSize);
            lock (_sync)
            {
                _latest = copy;
                _frames++;
            }
        }

        /// <summary>Computes brightness/colour statistics over the most recent frame.</summary>
        public FrameStats Analyze()
        {
            byte[]? frame;
            long frames;
            lock (_sync)
            {
                frame = _latest;
                frames = _frames;
            }

            if (frame == null)
            {
                return new FrameStats(0, 0, 0, 0, 0, 0, frames);
            }

            long nonDark = 0;
            long sumR = 0, sumG = 0, sumB = 0;
            int minLuma = 255;
            int maxLuma = 0;
            int pixels = Width * Height;
            for (int i = 0; i < pixels; i++)
            {
                int o = i * 4;
                byte b = frame[o];
                byte g = frame[o + 1];
                byte r = frame[o + 2];
                sumR += r; sumG += g; sumB += b;
                int luma = (299 * r + 587 * g + 114 * b) / 1000;
                if (luma < minLuma)
                {
                    minLuma = luma;
                }
                if (luma > maxLuma)
                {
                    maxLuma = luma;
                }
                if (luma > 16)
                {
                    nonDark++;
                }
            }

            byte avgR = (byte)(sumR / pixels);
            byte avgG = (byte)(sumG / pixels);
            byte avgB = (byte)(sumB / pixels);
            int avgChannelSpread = Math.Max(avgR, Math.Max(avgG, avgB)) - Math.Min(avgR, Math.Min(avgG, avgB));

            return new FrameStats(
                (double)nonDark / pixels,
                avgR, avgG, avgB,
                maxLuma - minLuma,
                avgChannelSpread,
                frames);
        }

        /// <summary>Writes the most recent frame to a 24-bit BMP for manual inspection.</summary>
        public bool WriteBmp(string path)
        {
            byte[]? frame;
            lock (_sync) { frame = _latest; }
            if (frame == null)
            {
                return false;
            }

            int rowSize = (Width * 3 + 3) & ~3;
            int imageSize = rowSize * Height;
            int fileSize = 54 + imageSize;

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var w = new BinaryWriter(fs);
            w.Write((byte)'B'); w.Write((byte)'M');
            w.Write(fileSize); w.Write(0); w.Write(54);
            w.Write(40); w.Write(Width); w.Write(Height);
            w.Write((short)1); w.Write((short)24);
            w.Write(0); w.Write(imageSize);
            w.Write(2835); w.Write(2835); w.Write(0); w.Write(0);

            var row = new byte[rowSize];
            for (int y = Height - 1; y >= 0; y--) // BMP is bottom-up
            {
                for (int x = 0; x < Width; x++)
                {
                    int o = (y * Width + x) * 4;
                    row[x * 3] = frame[o];         // B
                    row[x * 3 + 1] = frame[o + 1]; // G
                    row[x * 3 + 2] = frame[o + 2]; // R
                }
                w.Write(row, 0, rowSize);
            }
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Marshal.FreeHGlobal(_buffer);
        }
    }
}
