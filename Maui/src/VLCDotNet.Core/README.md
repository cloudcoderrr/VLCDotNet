# VLCDotNet3.Playback.Core

High-level, resource-safe C# wrappers over the playback-only **libvlc 3.x**
bindings shipped by [`VLCDotNet3.Playback`](https://github.com/cloudcoderrr/VLCDotNet).
Where `VLCDotNet3.Playback` exposes the raw `libvlc_*` P/Invoke surface, this
package adds an object-oriented, `IDisposable` API that manages every native
handle, callback delegate and buffer for you.

This package contains **no native code**. It takes a `PackageReference` on
`VLCDotNet3.Playback`, which supplies both the managed P/Invoke assembly and the
native libvlc runtimes for all 17 supported runtime identifiers (Windows, Linux,
macOS, Mac Catalyst, iOS and Android). That dependency flows transitively, so a
single package reference gives an app the managed API *and* the native engine.

## Types

| Type | Purpose |
|------|---------|
| `LibVLC` | The engine instance. Create it once; dispose it last. |
| `Media` | A file/URL/stream descriptor with parsing, metadata and track enumeration. |
| `MediaPlayer` | Transport, timing, audio, tracks, snapshots, native drawables and events. |
| `MemoryVideoSource` | Decode-to-memory frames for portable rendering + snapshot verification. |
| `MediaList` | Thread-safe ordered collection of `Media`. |
| `Equalizer` | Audio equalizer (flat or preset). |
| `MediaTrack` / `TrackDescription` | Decoded elementary-stream info. |

## Resource safety

Every native object is held in a `SafeHandle` subtype whose finalizer calls the
matching `libvlc_*_release`, so handles are released exactly once even if a
wrapper is leaked. Callback delegates handed to libvlc (events, video output,
logging) are stored in fields for the lifetime of the native registration, and
`MediaPlayer.Dispose` stops playback and detaches callbacks **before** releasing
the player and freeing the video buffer, avoiding native use-after-free.

## Quick start

```csharp
using VLCDotNet;

using var libvlc = new LibVLC("--no-video-title-show");
using var media = new Media(libvlc, "https://example.com/video.mp4", FromType.FromLocation);
using var player = new MediaPlayer(media);

player.EndReached += (_, _) => Console.WriteLine("done");
player.Play();
```

For an on-screen video surface, use **VLCDotNet3.Playback.Avalonia** or
**VLCDotNet3.Playback.Maui**, which host a `MediaPlayer` in a native view.

## License

Repository-authored C# bindings and wrappers: **MIT**. Redistributed VLC
binaries keep their upstream licenses. Not affiliated with VideoLAN.
