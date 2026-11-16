#if IOS || MACCATALYST
using System;
using Microsoft.Maui.Handlers;
using UIKit;
using VLCDotNet;

namespace VLCDotNet.Maui
{
    // Apple platforms render through the native UIView drawable, giving
    // hardware-accelerated VideoToolbox output.
    public partial class VideoViewHandler : ViewHandler<VideoView, UIView>
    {
        private MediaPlayer? _player;

        protected override UIView CreatePlatformView() => new UIView
        {
            BackgroundColor = UIColor.Black,
            ContentMode = UIViewContentMode.ScaleAspectFit,
        };

        protected override void ConnectHandler(UIView platformView)
        {
            base.ConnectHandler(platformView);
            OnMediaPlayerChanged();
        }

        protected override void DisconnectHandler(UIView platformView)
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
            if (player is null || PlatformView is null)
            {
                return;
            }

            player.NSObject = PlatformView.Handle;
        }

        private void Detach()
        {
            if (_player is not null)
            {
                try
                {
                    _player.NSObject = IntPtr.Zero;
                }
                catch (ObjectDisposedException)
                {
                }

                _player = null;
            }
        }
    }
}
#endif
