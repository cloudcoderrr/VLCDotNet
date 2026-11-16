# VLCDotNet3.Playback

Playback-only, cross-platform **libvlc 3.0.x** P/Invoke bindings for .NET,
packaged with native VLC runtimes **built from VideoLAN source in CI**. This is
the encode-free sibling of [`VLCDotNet3`](https://github.com/cloudcoderrr/VLCDotNet):
same managed API, same formats for **playback**, but the native build ships **no
software encoders** — `x264`, `x265` and `twolame` are removed from the
dependency closure (`VLC_PROFILE=playback`), so the package cannot software-encode
H.264/H.265/MP2. All decoders, demuxers, subtitle, metadata, disc and network
(TLS/RTSP/SMB/…) modules remain, so every format still **plays**.

The managed bindings are MIT-licensed; the redistributed VLC binaries keep their
upstream GPL/LGPL licenses (see `THIRD-PARTY-NOTICES.md`). Not affiliated with
VideoLAN.

## When to use this package

Choose **VLCDotNet3.Playback** when your app only needs to *play* media and you
want a smaller, encoder-free runtime (e.g. players, kiosks, viewers). Choose the
full **VLCDotNet3** when you also need software transcoding/encoding or audio
fingerprinting.

| | VLCDotNet3 | VLCDotNet3.Playback |
|---|---|---|
| Decode / play all formats | ✅ | ✅ |
| Subtitles, metadata, disc, network | ✅ | ✅ |
| Software H.264/H.265 encode (`x264`/`x265`) | ✅ | ❌ (removed from the build) |
| MP2 encode (`twolame`) | ✅ | ❌ |

The managed API, namespaces and `DllImport` targets are identical, so switching
packages is a one-line `PackageReference` change.

## Supported platforms

Native binaries ship for the same **6 OS families / 17 runtime IDs** as
VLCDotNet3:

| OS | RIDs |
|----|------|
| Windows | `win-x64`, `win-x86`, `win-arm64` |
| Linux | `linux-x64`, `linux-x86`, `linux-arm`, `linux-arm64` |
| macOS | `osx-x64`, `osx-arm64` |
| Mac Catalyst | `maccatalyst-x64`, `maccatalyst-arm64` |
| iOS | `ios-arm64`, `iossimulator-x64`, `iossimulator-arm64` |
| Android | `android-arm`, `android-arm64`, `android-x64` |

Consumable from **.NET MAUI**, **WinUI 3**, **Win32 / WPF / WinForms**,
**Avalonia** and console apps. Desktop resolves `libvlc.{dll,so,dylib}` by name;
Android ships `libvlc.so` as an `AndroidNativeLibrary`; Apple mobile embeds the
signed dynamic `VLCDotNet.framework`.

## How it is built

The playback runtimes are produced by the dedicated CI workflows
(`contribs-playback-vlc3`, `build-playback-vlc3`), which reuse the repository's
from-source native pipeline with `VLC_PROFILE=playback`:

1. The contrib dependency closure is rebuilt **without** the `x264`, `x265` and
   `twolame` encoder libraries and committed to `contribs/vlc-3.0-playback/<rid>/`.
2. libvlc is linked against that closure; any residual encoder plugin is stripped
   at delivery by the module policy in [`build/lib.sh`](../build/lib.sh).

The managed project ([`VLCDotNet.Playback.csproj`](VLCDotNet.Playback.csproj))
compiles the same sources as `src/VLCDotNet` and packs them with the playback
native runtimes.

## License

Repository-authored C# bindings and packaging: **MIT** (`LICENSE`). Redistributed
VLC binaries, plugins and other third-party components keep their upstream
licenses; see `THIRD-PARTY-NOTICES.md`. Not affiliated with VideoLAN.
