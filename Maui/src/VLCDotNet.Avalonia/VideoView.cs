using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace VLCDotNet.Avalonia
{
    /// <summary>How a <see cref="VideoView"/> presents decoded video.</summary>
    public enum VideoViewRenderMode
    {
        /// <summary>Native handle on desktop/Apple, memory on Android (recommended).</summary>
        Auto,

        /// <summary>Hardware-accelerated native-window output (HWND / X11 / NSView).</summary>
        NativeHandle,

        /// <summary>Portable in-memory rendering into a <see cref="WriteableBitmap"/>.</summary>
        Memory,
    }

    /// <summary>
    /// An Avalonia control that renders the video of a <see cref="MediaPlayer"/>.
    /// It intentionally has <b>no transport controls</b> - compose your own UI on
    /// top by placing this view in a panel with your controls above it.
    /// </summary>
    /// <remarks>
    /// On Windows, Linux (X11) and macOS the view hosts a native child window and
    /// hands it to libvlc for hardware-accelerated output. On Android (and any
    /// platform where a native handle is unavailable) it renders decoded frames
    /// into a <see cref="WriteableBitmap"/>. Set <see cref="MediaPlayer"/>, then
    /// call <c>Play()</c> on it.
    /// </remarks>
    public class VideoView : Decorator
    {
        public static readonly StyledProperty<MediaPlayer?> MediaPlayerProperty =
            AvaloniaProperty.Register<VideoView, MediaPlayer?>(nameof(MediaPlayer));

        public static readonly StyledProperty<VideoViewRenderMode> RenderModeProperty =
            AvaloniaProperty.Register<VideoView, VideoViewRenderMode>(nameof(RenderMode), VideoViewRenderMode.Auto);

        public static readonly StyledProperty<int> MemoryRenderWidthProperty =
            AvaloniaProperty.Register<VideoView, int>(nameof(MemoryRenderWidth), 640);

        public static readonly StyledProperty<int> MemoryRenderHeightProperty =
            AvaloniaProperty.Register<VideoView, int>(nameof(MemoryRenderHeight), 360);

        private NativeVideoHost? _nativeHost;
        private Image? _image;
        private WriteableBitmap? _bitmap;
        private MemoryVideoSource? _memorySource;
        private MediaPlayer? _attachedPlayer;
        private bool _attached;

        /// <summary>The player whose video is shown. Set this, then call <c>Play()</c> on it.</summary>
        public MediaPlayer? MediaPlayer
        {
            get => GetValue(MediaPlayerProperty);
            set => SetValue(MediaPlayerProperty, value);
        }

        /// <summary>Selects the rendering strategy (default <see cref="VideoViewRenderMode.Auto"/>).</summary>
        public VideoViewRenderMode RenderMode
        {
            get => GetValue(RenderModeProperty);
            set => SetValue(RenderModeProperty, value);
        }

        /// <summary>Back-buffer width used in memory rendering mode.</summary>
        public int MemoryRenderWidth
        {
            get => GetValue(MemoryRenderWidthProperty);
            set => SetValue(MemoryRenderWidthProperty, value);
        }

        /// <summary>Back-buffer height used in memory rendering mode.</summary>
        public int MemoryRenderHeight
        {
            get => GetValue(MemoryRenderHeightProperty);
            set => SetValue(MemoryRenderHeightProperty, value);
        }

        /// <summary>The effective mode chosen for the current player/platform.</summary>
        public VideoViewRenderMode EffectiveRenderMode { get; private set; }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == MediaPlayerProperty)
            {
                Teardown();
                if (_attached)
                {
                    Setup();
                }
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _attached = true;
            Setup();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            Teardown();
            _attached = false;
            base.OnDetachedFromVisualTree(e);
        }

        private void Setup()
        {
            MediaPlayer? player = MediaPlayer;
            if (player is null || ReferenceEquals(_attachedPlayer, player))
            {
                return;
            }

            _attachedPlayer = player;
            EffectiveRenderMode = ResolveMode();
            if (EffectiveRenderMode == VideoViewRenderMode.Memory)
            {
                SetupMemory(player);
            }
            else
            {
                SetupNative(player);
            }
        }

        private VideoViewRenderMode ResolveMode()
        {
            if (RenderMode != VideoViewRenderMode.Auto)
            {
                return RenderMode;
            }

            // Native-window output works on Windows, X11 and Apple. Android has no
            // public AWindow glue in this build, so use the portable renderer.
            if (OperatingSystem.IsAndroid())
            {
                return VideoViewRenderMode.Memory;
            }

            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux()
                || OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
            {
                return VideoViewRenderMode.NativeHandle;
            }

            return VideoViewRenderMode.Memory;
        }

        private void SetupNative(MediaPlayer player)
        {
            _nativeHost = new NativeVideoHost
            {
                HandleCreated = h => AttachDrawable(player, h),
                HandleDestroying = () => DetachDrawable(player),
            };
            Child = _nativeHost;
        }

        private static void AttachDrawable(MediaPlayer player, IPlatformHandle handle)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    player.Hwnd = handle.Handle;
                }
                else if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
                {
                    player.NSObject = handle.Handle;
                }
                else if (OperatingSystem.IsLinux())
                {
                    player.XWindow = (uint)handle.Handle.ToInt64();
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static void DetachDrawable(MediaPlayer player)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    player.Hwnd = IntPtr.Zero;
                }
                else if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
                {
                    player.NSObject = IntPtr.Zero;
                }
                else if (OperatingSystem.IsLinux())
                {
                    player.XWindow = 0;
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void SetupMemory(MediaPlayer player)
        {
            int w = Math.Max(16, MemoryRenderWidth);
            int h = Math.Max(16, MemoryRenderHeight);
            _bitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            _image = new Image { Stretch = Stretch.Uniform, Source = _bitmap };
            Child = _image;

            _memorySource = player.EnableMemoryRendering(w, h, VideoPixelFormat.Bgra);
            _memorySource.FrameAvailable += OnFrameAvailable;
        }

        private void OnFrameAvailable(object? sender, EventArgs e)
        {
            MemoryVideoSource? source = _memorySource;
            if (source is null)
            {
                return;
            }

            Dispatcher.UIThread.Post(() => RenderFrame(source), DispatcherPriority.Render);
        }

        private void RenderFrame(MemoryVideoSource source)
        {
            WriteableBitmap? bmp = _bitmap;
            Image? image = _image;
            if (bmp is null || image is null)
            {
                return;
            }

            using (ILockedFramebuffer fb = bmp.Lock())
            {
                if (fb.RowBytes == source.Stride && fb.Size.Height == source.Height)
                {
                    source.TryCopyLatestFrame(fb.Address, fb.RowBytes * fb.Size.Height);
                }
                else
                {
                    byte[]? frame = source.GetLatestFrame();
                    if (frame is not null)
                    {
                        int rows = Math.Min(fb.Size.Height, source.Height);
                        int bytes = Math.Min(fb.RowBytes, source.Stride);
                        for (int y = 0; y < rows; y++)
                        {
                            Marshal.Copy(frame, y * source.Stride, fb.Address + (y * fb.RowBytes), bytes);
                        }
                    }
                }
            }

            image.InvalidateVisual();
        }

        private void Teardown()
        {
            if (_memorySource is not null)
            {
                _memorySource.FrameAvailable -= OnFrameAvailable;
                // The MemoryVideoSource is owned and disposed by its MediaPlayer.
                _memorySource = null;
            }

            if (_nativeHost is not null)
            {
                _nativeHost.HandleCreated = null;
                _nativeHost.HandleDestroying = null;
                _nativeHost = null;
            }

            _image = null;
            _bitmap = null;
            Child = null;
            _attachedPlayer = null;
        }
    }
}
