using VLCDotNet.Maui;

namespace VLCDotNet.ViewTests.Maui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseVlcVideoView();
            return builder.Build();
        }
    }
}
