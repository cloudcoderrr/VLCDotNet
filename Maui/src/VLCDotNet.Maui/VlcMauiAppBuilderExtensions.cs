using Microsoft.Maui.Hosting;

namespace VLCDotNet.Maui
{
    /// <summary>Registration helpers for the VLCDotNet MAUI video view.</summary>
    public static class VlcMauiAppBuilderExtensions
    {
        /// <summary>
        /// Registers the <see cref="VideoViewHandler"/> so <see cref="VideoView"/>
        /// can be used in your MAUI app. Call in <c>MauiProgram.CreateMauiApp</c>:
        /// <c>builder.UseVlcVideoView();</c>
        /// </summary>
        public static MauiAppBuilder UseVlcVideoView(this MauiAppBuilder builder)
        {
            builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<VideoView, VideoViewHandler>());
            return builder;
        }
    }
}
