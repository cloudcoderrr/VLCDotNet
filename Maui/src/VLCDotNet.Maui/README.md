# VLCDotNet3.Playback.Maui

A **.NET MAUI** `VideoView` control that plays any media format with libvlc 3.x
on **Android, iOS and Mac Catalyst**, built on
[`VLCDotNet3.Playback.Core`](https://github.com/cloudcoderrr/VLCDotNet).

- **Hardware-accelerated** native (VideoToolbox) output on iOS and Mac Catalyst.
- **Portable bitmap** rendering on Android.
- **No built-in controls** - overlay your own transport UI on top.

## Setup

Register the handler in `MauiProgram.cs`:

```csharp
using VLCDotNet.Maui;

public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>()
        .UseVlcVideoView();   // <-- registers the VideoView handler
    return builder.Build();
}
```

## Usage

```xml
<Grid xmlns:vlc="clr-namespace:VLCDotNet.Maui;assembly=VLCDotNet.Maui">
  <vlc:VideoView x:Name="Video" />
  <!-- Your own controls, drawn above the video -->
  <Button Text="Pause" VerticalOptions="End" HorizontalOptions="Center" />
</Grid>
```

```csharp
using VLCDotNet;

_libvlc = new LibVLC("--no-video-title-show");
_player = new MediaPlayer(_libvlc);
Video.MediaPlayer = _player;

_player.Media = new Media(_libvlc, "https://example.com/video.mp4", FromType.FromLocation);
_player.Play();
```

The native libvlc runtimes are delivered transitively by
`VLCDotNet3.Playback.Core` → `VLCDotNet3.Playback`.

## License

Repository-authored C# bindings and control: **MIT**. Redistributed VLC binaries
keep their upstream licenses. Not affiliated with VideoLAN.
