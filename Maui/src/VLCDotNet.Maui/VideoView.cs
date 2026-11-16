using Microsoft.Maui.Controls;
using VlcMediaPlayer = VLCDotNet.MediaPlayer;

namespace VLCDotNet.Maui
{
    /// <summary>
    /// A .NET MAUI control that renders the video of a libvlc
    /// <see cref="VLCDotNet.MediaPlayer"/>. It has <b>no transport controls</b> -
    /// overlay your own UI on top by placing it in a layout with your controls
    /// above it. Register the handler with
    /// <see cref="VlcMauiAppBuilderExtensions.UseVlcVideoView"/>.
    /// </summary>
    public class VideoView : View
    {
        public static readonly BindableProperty MediaPlayerProperty =
            BindableProperty.Create(nameof(MediaPlayer), typeof(VlcMediaPlayer), typeof(VideoView), null);

        public static readonly BindableProperty RenderModeProperty =
            BindableProperty.Create(nameof(RenderMode), typeof(VideoViewRenderMode), typeof(VideoView), VideoViewRenderMode.Auto);

        public static readonly BindableProperty MemoryRenderWidthProperty =
            BindableProperty.Create(nameof(MemoryRenderWidth), typeof(int), typeof(VideoView), 640);

        public static readonly BindableProperty MemoryRenderHeightProperty =
            BindableProperty.Create(nameof(MemoryRenderHeight), typeof(int), typeof(VideoView), 360);

        /// <summary>The player whose video is shown. Set this, then call <c>Play()</c> on it.</summary>
        public VlcMediaPlayer? MediaPlayer
        {
            get => (VlcMediaPlayer?)GetValue(MediaPlayerProperty);
            set => SetValue(MediaPlayerProperty, value);
        }

        /// <summary>Advisory render mode (platform default is used automatically).</summary>
        public VideoViewRenderMode RenderMode
        {
            get => (VideoViewRenderMode)GetValue(RenderModeProperty);
            set => SetValue(RenderModeProperty, value);
        }

        /// <summary>Back-buffer width used by the bitmap rendering path (Android/Windows).</summary>
        public int MemoryRenderWidth
        {
            get => (int)GetValue(MemoryRenderWidthProperty);
            set => SetValue(MemoryRenderWidthProperty, value);
        }

        /// <summary>Back-buffer height used by the bitmap rendering path (Android/Windows).</summary>
        public int MemoryRenderHeight
        {
            get => (int)GetValue(MemoryRenderHeightProperty);
            set => SetValue(MemoryRenderHeightProperty, value);
        }
    }
}
