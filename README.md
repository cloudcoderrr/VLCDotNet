# VLCDotNet

Cross-platform **libvlc** P/Invoke bindings for .NET, packaged with native VLC
binaries built from source for 14 targets.

Two NuGet packages are produced:

| Package | libvlc | Status |
|---------|--------|--------|
| `VLCDotNet3` | VLC **3.0.x** (stable) | primary |
| `VLCDotNet4` | VLC **4.0.x** (preview) | after v3 ships |

## Supported platforms (native binaries)

| OS | Architectures |
|----|---------------|
| Windows | x64, arm64 |
| Linux | x64, arm, arm64 |
| macOS | x64, arm64 |
| Mac Catalyst | x64, arm64 |
| iOS | device (arm64) + simulator (x64, arm64) |
| Android | armeabi-v7a (arm), arm64-v8a, x86_64 |

Consumable from **.NET MAUI**, **WinUI 3**, **Win32/WPF/WinForms**, **Avalonia**
and plain console apps on Linux/macOS/Windows.

## Repository layout

```
src/VLCDotNet/         Managed libvlc P/Invoke bindings (netstandard2.0 + mobile heads)
nuget/VLCDotNet3/      NuGet packing project (managed lib + all native binaries)
build/                 Cross-platform VLC build scripts used by CI
patches/vlc-3.0/       Unified-diff patches applied to VLC source before building
tests/                 Avalonia (desktop) + MAUI (mobile) test apps + shared test suite
.github/workflows/     Source-build + test CI
Tests/Data/            Synthetic media test pack (video/audio/subtitles)
```

## Building the native binaries

The native VLC binaries are produced entirely in CI from VideoLAN source. See
[`.github/workflows`](.github/workflows) and [`build/README.md`](build/README.md).

## License

The repository-authored C# bindings and packaging code are licensed under the
**MIT License**; see [`LICENSE`](LICENSE).

Redistributed VLC binaries, plugins, and other third-party components keep
their upstream licenses. See [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)
for the applicable VLC/libvlc GPL/LGPL notices. Not affiliated with VideoLAN.