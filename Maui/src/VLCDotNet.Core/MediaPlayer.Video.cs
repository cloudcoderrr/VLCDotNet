using System;

namespace VLCDotNet
{
    public sealed partial class MediaPlayer
    {
        private MemoryVideoSource? _videoSource;

        // ----- Native drawables (hardware-accelerated output) --------------

        /// <summary>
        /// The Win32 <c>HWND</c> libvlc renders video into. Set this to a native
        /// window handle for hardware-accelerated output on Windows.
        /// </summary>
        public IntPtr Hwnd
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_player_get_hwnd(NativePointer);
            }
            set
            {
                ThrowIfDisposed();
                LibVlc.libvlc_media_player_set_hwnd(NativePointer, value);
            }
        }

        /// <summary>
        /// The X11 window id libvlc renders video into (Linux). Set this for
        /// hardware-accelerated output under X11.
        /// </summary>
        public uint XWindow
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_player_get_xwindow(NativePointer);
            }
            set
            {
                ThrowIfDisposed();
                LibVlc.libvlc_media_player_set_xwindow(NativePointer, value);
            }
        }

        /// <summary>
        /// The macOS/iOS <c>NSView</c>/<c>UIView</c> (or <c>CALayer</c>) libvlc
        /// renders video into. Set this for hardware-accelerated output on Apple
        /// platforms (VideoToolbox).
        /// </summary>
        public IntPtr NSObject
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_player_get_nsobject(NativePointer);
            }
            set
            {
                ThrowIfDisposed();
                LibVlc.libvlc_media_player_set_nsobject(NativePointer, value);
            }
        }

        // ----- Geometry ----------------------------------------------------

        /// <summary>Video scaling factor (0 = auto/fit to the output).</summary>
        public float Scale
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_video_get_scale(NativePointer);
            }
            set
            {
                ThrowIfDisposed();
                LibVlc.libvlc_video_set_scale(NativePointer, value);
            }
        }

        /// <summary>Forced aspect ratio (e.g. "16:9"), or <see langword="null"/> for source.</summary>
        public string? AspectRatio
        {
            get
            {
                ThrowIfDisposed();
                IntPtr ptr = LibVlc.libvlc_video_get_aspect_ratio(NativePointer);
                try
                {
                    return LibVlc.Utf8ToString(ptr);
                }
                finally
                {
                    if (ptr != IntPtr.Zero)
                    {
                        LibVlc.libvlc_free(ptr);
                    }
                }
            }
            set
            {
                ThrowIfDisposed();
                LibVlc.libvlc_video_set_aspect_ratio(NativePointer, value!);
            }
        }

        /// <summary>Sets the deinterlace mode (e.g. "blend", "yadif"), or <see langword="null"/> to disable.</summary>
        public void SetDeinterlace(string? mode)
        {
            ThrowIfDisposed();
            LibVlc.libvlc_video_set_deinterlace(NativePointer, mode!);
        }

        // ----- Snapshot / size ---------------------------------------------

        /// <summary>
        /// Writes a snapshot of video output <paramref name="num"/> to
        /// <paramref name="filePath"/>. Pass 0 for width/height to keep the source
        /// size, or one of them to preserve aspect ratio.
        /// </summary>
        public bool TakeSnapshot(uint num, string filePath, uint width, uint height)
        {
            ThrowIfDisposed();
            return LibVlc.libvlc_video_take_snapshot(NativePointer, num, filePath, width, height) == 0;
        }

        /// <summary>Gets the pixel size of video output <paramref name="num"/>, or <see langword="null"/>.</summary>
        public (uint Width, uint Height)? GetVideoSize(uint num = 0)
        {
            ThrowIfDisposed();
            if (LibVlc.libvlc_video_get_size(NativePointer, num, out uint w, out uint h) == 0)
            {
                return (w, h);
            }

            return null;
        }

        // ----- Memory (custom) video output --------------------------------

        /// <summary>
        /// Routes decoded frames to an in-memory <see cref="MemoryVideoSource"/>
        /// instead of a native window. This is the portable rendering path used by
        /// the view controls where a native drawable is unavailable, and it makes
        /// frames available for inspection/snapshotting. The player owns and
        /// disposes the returned source.
        /// </summary>
        public MemoryVideoSource EnableMemoryRendering(int width, int height, VideoPixelFormat format = VideoPixelFormat.Bgra)
        {
            ThrowIfDisposed();
            lock (_sync)
            {
                _videoSource?.Dispose();
                _videoSource = new MemoryVideoSource(this, width, height, format);
                return _videoSource;
            }
        }

        /// <summary>The active memory video source, if <see cref="EnableMemoryRendering"/> was called.</summary>
        public MemoryVideoSource? VideoSource => _videoSource;

        // Low-level pass-throughs used by MemoryVideoSource. The caller is
        // responsible for keeping the supplied delegates alive for the player's
        // lifetime (MemoryVideoSource stores them in fields).
        internal void SetVideoFormat(string chroma, uint width, uint height, uint pitch)
            => LibVlc.libvlc_video_set_format(NativePointer, chroma, width, height, pitch);

        internal void SetVideoCallbacks(VlcVideoLockCb @lock, VlcVideoUnlockCb? unlock, VlcVideoDisplayCb? display, IntPtr opaque)
            => LibVlc.libvlc_video_set_callbacks(NativePointer, @lock, unlock, display, opaque);

        private void DisposeVideoOutput()
        {
            // Called from Dispose AFTER the player has been stopped and released,
            // so no decode/display callback can still touch the frame buffer.
            MemoryVideoSource? source;
            lock (_sync)
            {
                source = _videoSource;
                _videoSource = null;
            }

            source?.Dispose();
        }
    }
}
