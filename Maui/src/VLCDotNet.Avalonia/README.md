# VLCDotNet3.Playback.Avalonia

An **Avalonia** `VideoView` control that plays any media format with libvlc 3.x,
built on [`VLCDotNet3.Playback.Core`](https://github.com/cloudcoderrr/VLCDotNet).

- **Hardware-accelerated** native-window output on Windows (HWND), Linux (X11)
  and macOS (NSView), and native UIView output on iOS/Mac Catalyst.
- **Portable** `WriteableBitmap` rendering mode for Android and headless
  scenarios, selected automatically.
- **No built-in controls** - overlay your own transport UI on top.

## Usage

```xml
<Panel>
  <vlc:VideoView x:Name="Video" />
  <!-- Your own controls, drawn above the video -->
  <Button Content="Pause" HorizontalAlignment="Center" VerticalAlignment="Bottom" />
</Panel>
```

```csharp
using VLCDotNet;

_libvlc = new LibVLC("--no-video-title-show");
_player = new MediaPlayer(_libvlc);
Video.MediaPlayer = _player;

_player.Media = new Media(_libvlc, "https://example.com/video.mp4", FromType.FromLocation);
_player.Play();
```

Add `xmlns:vlc="using:VLCDotNet.Avalonia"` to your XAML.

The native libvlc runtimes are delivered transitively by
`VLCDotNet3.Playback.Core` → `VLCDotNet3.Playback`, so no extra setup is needed.

## License

Repository-authored C# bindings and control: **MIT**. Redistributed VLC binaries
keep their upstream licenses. Not affiliated with VideoLAN.
