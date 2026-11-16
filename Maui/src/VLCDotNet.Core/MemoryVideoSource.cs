using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    /// <summary>
    /// Receives decoded video frames from libvlc through the decode-to-memory
    /// (vmem) callbacks and exposes them as a tightly packed 32-bit pixel buffer.
    /// This is the portable rendering backbone for the Avalonia/MAUI video views
    /// (used where a native drawable is not available) and the basis for
    /// headless snapshot verification.
    /// </summary>
    /// <remarks>
    /// Create via <see cref="MediaPlayer.EnableMemoryRendering"/>; the owning
    /// player disposes it in the correct order (after playback is stopped and the
    /// player released), so the frame buffer is never freed while a decode thread
    /// could still write to it.
    /// </remarks>
    public sealed class MemoryVideoSource : IDisposable
    {
        private readonly VideoPixelFormat _format;
        private readonly int _rOff;
        private readonly int _gOff;
        private readonly int _bOff;
        private readonly IntPtr _buffer;
        private readonly int _bufferSize;
        private readonly object _sync = new object();

        // Kept alive for the player's lifetime so libvlc's native function
        // pointers never dangle.
        private readonly VlcVideoLockCb _lockCb;
        private readonly VlcVideoUnlockCb _unlockCb;
        private readonly VlcVideoDisplayCb _displayCb;

        private byte[]? _latest;
        private long _frames;
        private bool _disposed;

        internal MemoryVideoSource(MediaPlayer player, int width, int height, VideoPixelFormat format)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive.");
            }

            Width = width;
            Height = height;
            Stride = width * 4;
            _format = format;
            (_rOff, _gOff, _bOff, _) = format.Offsets();
            _bufferSize = Stride * height;
            _buffer = Marshal.AllocHGlobal(_bufferSize);

            _lockCb = OnLock;
            _unlockCb = OnUnlock;
            _displayCb = OnDisplay;

            player.SetVideoFormat(format.ToChroma(), (uint)width, (uint)height, (uint)Stride);
            player.SetVideoCallbacks(_lockCb, _unlockCb, _displayCb, IntPtr.Zero);
        }

        /// <summary>Raised on a libvlc thread when a new frame has been displayed.</summary>
        public event EventHandler? FrameAvailable;

        public int Width { get; }

        public int Height { get; }

        /// <summary>Row stride in bytes (<see cref="Width"/> * 4).</summary>
        public int Stride { get; }

        /// <summary>The requested pixel layout.</summary>
        public VideoPixelFormat Format => _format;

        /// <summary>Total number of frames displayed so far.</summary>
        public long FrameCount
        {
            get
            {
                lock (_sync)
                {
                    return _frames;
                }
            }
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
            try
            {
                var copy = new byte[_bufferSize];
                Marshal.Copy(_buffer, copy, 0, _bufferSize);
                lock (_sync)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _latest = copy;
                    _frames++;
                }

                FrameAvailable?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // Never let a managed exception cross back into native code.
            }
        }

        /// <summary>
        /// Copies the most recent frame into <paramref name="destination"/> (which
        /// must be at least <see cref="Stride"/> * <see cref="Height"/> bytes).
        /// Returns <see langword="false"/> when no frame has been displayed yet.
        /// </summary>
        public bool TryCopyLatestFrame(Span<byte> destination)
        {
            lock (_sync)
            {
                if (_latest is null || destination.Length < _bufferSize)
                {
                    return false;
                }

                _latest.AsSpan(0, _bufferSize).CopyTo(destination);
                return true;
            }
        }

        /// <summary>
        /// Copies the most recent frame into unmanaged memory at
        /// <paramref name="destination"/> (e.g. a locked bitmap back-buffer).
        /// Returns <see langword="false"/> when no frame is available.
        /// </summary>
        public bool TryCopyLatestFrame(IntPtr destination, int destinationLength)
        {
            lock (_sync)
            {
                if (_latest is null || destinationLength < _bufferSize)
                {
                    return false;
                }

                Marshal.Copy(_latest, 0, destination, _bufferSize);
                return true;
            }
        }

        /// <summary>Returns a copy of the most recent frame, or <see langword="null"/>.</summary>
        public byte[]? GetLatestFrame()
        {
            lock (_sync)
            {
                return _latest is null ? null : (byte[])_latest.Clone();
            }
        }

        /// <summary>Computes brightness/colour statistics over the most recent frame.</summary>
        public VideoFrameStats Analyze()
        {
            byte[]? frame;
            long frames;
            lock (_sync)
            {
                frame = _latest;
                frames = _frames;
            }

            if (frame is null)
            {
                return new VideoFrameStats(frames, 0, 0, 0);
            }

            long nonDark = 0;
            long sumR = 0, sumG = 0, sumB = 0;
            int minLuma = 255;
            int maxLuma = 0;
            int pixels = Width * Height;
            for (int i = 0; i < pixels; i++)
            {
                int o = i * 4;
                byte r = frame[o + _rOff];
                byte g = frame[o + _gOff];
                byte b = frame[o + _bOff];
                sumR += r;
                sumG += g;
                sumB += b;
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
            int spread = Math.Max(avgR, Math.Max(avgG, avgB)) - Math.Min(avgR, Math.Min(avgG, avgB));

            return new VideoFrameStats(frames, (double)nonDark / pixels, maxLuma - minLuma, spread);
        }

        /// <summary>Writes the most recent frame to a 24-bit BMP (dependency-free). Returns false if no frame.</summary>
        public bool WriteBmp(string path)
        {
            byte[]? frame;
            lock (_sync)
            {
                frame = _latest;
            }

            if (frame is null)
            {
                return false;
            }

            int rowSize = (Width * 3 + 3) & ~3;
            int imageSize = rowSize * Height;
            int fileSize = 54 + imageSize;

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var w = new BinaryWriter(fs);
            w.Write((byte)'B');
            w.Write((byte)'M');
            w.Write(fileSize);
            w.Write(0);
            w.Write(54);
            w.Write(40);
            w.Write(Width);
            w.Write(Height);
            w.Write((short)1);
            w.Write((short)24);
            w.Write(0);
            w.Write(imageSize);
            w.Write(2835);
            w.Write(2835);
            w.Write(0);
            w.Write(0);

            var row = new byte[rowSize];
            for (int y = Height - 1; y >= 0; y--) // BMP is bottom-up
            {
                for (int x = 0; x < Width; x++)
                {
                    int o = (y * Width + x) * 4;
                    row[x * 3] = frame[o + _bOff];
                    row[x * 3 + 1] = frame[o + _gOff];
                    row[x * 3 + 2] = frame[o + _rOff];
                }

                w.Write(row, 0, rowSize);
            }

            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _latest = null;
            }

            Marshal.FreeHGlobal(_buffer);
        }
    }
}
