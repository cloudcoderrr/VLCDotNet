#if ANDROID
using System;
using Android.Graphics;
using Android.Widget;
using Java.Nio;
using Microsoft.Maui.Handlers;
using VLCDotNet;

namespace VLCDotNet.Maui
{
    // Android has no public AWindow glue in this build, so video is presented by
    // routing decoded frames through the portable memory renderer into a Bitmap.
    public partial class VideoViewHandler : ViewHandler<VideoView, ImageView>
    {
        private MediaPlayer? _player;
        private MemoryVideoSource? _source;
        private Bitmap? _bitmap;

        protected override ImageView CreatePlatformView() => new ImageView(Context)
        {
            // Preserve aspect ratio, letterboxed on a black background.
        };

        protected override void ConnectHandler(ImageView platformView)
        {
            base.ConnectHandler(platformView);
            platformView.SetBackgroundColor(Android.Graphics.Color.Black);
            platformView.SetScaleType(ImageView.ScaleType.FitCenter);
            OnMediaPlayerChanged();
        }

        protected override void DisconnectHandler(ImageView platformView)
        {
            Detach(platformView);
            base.DisconnectHandler(platformView);
        }

        private void OnMediaPlayerChanged()
        {
            MediaPlayer? player = VirtualView?.MediaPlayer;
            if (ReferenceEquals(player, _player))
            {
                return;
            }

            Detach(PlatformView);
            _player = player;
            if (player is null || VirtualView is null || PlatformView is null)
            {
                return;
            }

            int w = Math.Max(16, VirtualView.MemoryRenderWidth);
            int h = Math.Max(16, VirtualView.MemoryRenderHeight);
            _bitmap = Bitmap.CreateBitmap(w, h, Bitmap.Config.Argb8888!);
            _source = player.EnableMemoryRendering(w, h, VideoPixelFormat.Rgba);
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

            PlatformView?.Post(() =>
            {
                try
                {
                    Bitmap? bmp = _bitmap;
                    if (bmp is null || PlatformView is null)
                    {
                        return;
                    }

                    using var buffer = ByteBuffer.Wrap(frame);
                    bmp.CopyPixelsFromBuffer(buffer);
                    PlatformView.SetImageBitmap(bmp);
                }
                catch
                {
                    // best effort; a dropped frame is harmless
                }
            });
        }

        private void Detach(ImageView? platformView)
        {
            if (_source is not null)
            {
                _source.FrameAvailable -= OnFrame;
                // The source is owned and disposed by its MediaPlayer.
                _source = null;
            }

            platformView?.SetImageBitmap(null);
            _bitmap = null;
            _player = null;
        }
    }
}
#endif
