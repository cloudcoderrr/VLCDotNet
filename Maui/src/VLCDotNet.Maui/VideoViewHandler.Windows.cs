#if WINDOWS
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Media.Imaging;
using VLCDotNet;
using WImage = Microsoft.UI.Xaml.Controls.Image;

namespace VLCDotNet.Maui
{
    // Windows (WinUI 3) presents decoded frames through the portable memory
    // renderer into a WriteableBitmap.
    public partial class VideoViewHandler : ViewHandler<VideoView, WImage>
    {
        private MediaPlayer? _player;
        private MemoryVideoSource? _source;
        private WriteableBitmap? _bitmap;

        protected override WImage CreatePlatformView() => new WImage
        {
            Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
        };

        protected override void ConnectHandler(WImage platformView)
        {
            base.ConnectHandler(platformView);
            OnMediaPlayerChanged();
        }

        protected override void DisconnectHandler(WImage platformView)
        {
            Detach();
            base.DisconnectHandler(platformView);
        }

        private void OnMediaPlayerChanged()
        {
            MediaPlayer? player = VirtualView?.MediaPlayer;
            if (ReferenceEquals(player, _player))
            {
                return;
            }

            Detach();
            _player = player;
            if (player is null || VirtualView is null || PlatformView is null)
            {
                return;
            }

            int w = Math.Max(16, VirtualView.MemoryRenderWidth);
            int h = Math.Max(16, VirtualView.MemoryRenderHeight);
            _bitmap = new WriteableBitmap(w, h);
            PlatformView.Source = _bitmap;
            _source = player.EnableMemoryRendering(w, h, VideoPixelFormat.Bgra);
            _source.FrameAvailable += OnFrame;
        }

        private void OnFrame(object? sender, EventArgs e)
        {
            MemoryVideoSource? source = _source;
            byte[]? frame = source?.GetLatestFrame();
            if (frame is null)
            {
                return;
            }

            PlatformView?.DispatcherQueue?.TryEnqueue(() =>
            {
                try
                {
                    WriteableBitmap? bmp = _bitmap;
                    if (bmp is null)
                    {
                        return;
                    }

                    using (Stream stream = bmp.PixelBuffer.AsStream())
                    {
                        stream.Write(frame, 0, Math.Min(frame.Length, (int)bmp.PixelBuffer.Length));
                    }

                    bmp.Invalidate();
                }
                catch
                {
                    // best effort; a dropped frame is harmless
                }
            });
        }

        private void Detach()
        {
            if (_source is not null)
            {
                _source.FrameAvailable -= OnFrame;
                // The source is owned and disposed by its MediaPlayer.
                _source = null;
            }

            _bitmap = null;
            _player = null;
        }
    }
}
#endif
