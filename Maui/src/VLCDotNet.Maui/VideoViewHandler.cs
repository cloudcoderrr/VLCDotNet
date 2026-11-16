using Microsoft.Maui.Handlers;

namespace VLCDotNet.Maui
{
    /// <summary>
    /// MAUI handler for <see cref="VideoView"/>. The platform-specific partials
    /// provide the native view and bridge it to the libvlc player (native
    /// VideoToolbox output on Apple; bitmap rendering on Android/Windows).
    /// </summary>
    public partial class VideoViewHandler
    {
        public static readonly IPropertyMapper<VideoView, VideoViewHandler> VideoViewMapper =
            new PropertyMapper<VideoView, VideoViewHandler>(ViewMapper)
            {
                [nameof(VideoView.MediaPlayer)] = MapMediaPlayer,
                [nameof(VideoView.RenderMode)] = MapMediaPlayer,
            };

        public VideoViewHandler()
            : base(VideoViewMapper)
        {
        }

        public VideoViewHandler(IPropertyMapper? mapper)
            : base(mapper ?? VideoViewMapper)
        {
        }

        private static void MapMediaPlayer(VideoViewHandler handler, VideoView view) => handler.OnMediaPlayerChanged();
    }
}
