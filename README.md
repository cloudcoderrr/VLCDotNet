# VLCDotNet

Production-ready, cross-platform **libvlc** P/Invoke bindings for .NET, packaged
with native VLC runtimes **built from VideoLAN source in CI** for every supported
target. The managed bindings are MIT-licensed; the redistributed VLC binaries
keep their upstream GPL/LGPL licenses (see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)).
Not affiliated with VideoLAN.

## Packages

Two NuGet packages are produced and published on the repository's
[GitHub Releases](https://github.com/cloudcoderrr/VLCDotNet/releases) page:

| Package | libvlc | Status | Latest |
|---------|--------|--------|--------|
| `VLCDotNet3` | VLC **3.0.24** (Vetinari) | ✅ production | `v3.0.24.1` |
| `VLCDotNet3.Playback` | VLC **3.0.24**, playback-only (no encoders) | ✅ production | `vplay3-3.0.24.N` |
| `VLCDotNet4` | VLC **4.0 master** `f94b6117` | 🔄 preview | `v4.0.0-preview.N` |

The targeted VLC versions live in [`Directory.Build.props`](Directory.Build.props)
(`VlcVersion` / `VlcVersion4` + `VlcRef4`).

**`VLCDotNet3.Playback`** is the encode-free sibling of `VLCDotNet3`: identical
managed API and the same decoders/demuxers for playback of every format, but the
native build ships **no software encoders** — `x264`, `x265` and `twolame` are
pruned from the contrib closure (`VLC_PROFILE=playback`). The thin packaging
project lives in [`PlaybackOnly/`](PlaybackOnly).

## Supported platforms

Native binaries ship for **6 OS families / 17 runtime IDs** for VLC 3 and **16
RIDs** for VLC 4 (see note below). WinUI 3 and Win32/WPF/WinForms consume the
same `win-x64` / `win-arm64` runtimes.

| OS | Architectures | RIDs | VLC3 | VLC4 |
|----|---------------|------|------|------|
| Windows | x64, **x86**, arm64 | `win-x64`, `win-x86`, `win-arm64` | ✅ | ✅ (no x86) |
| Linux | x64, **x86**, arm, arm64 | `linux-x64`, `linux-x86`, `linux-arm`, `linux-arm64` | ✅ | ✅ |
| macOS | x64, arm64 | `osx-x64`, `osx-arm64` | ✅ | ✅ |
| Mac Catalyst | x64, arm64 | `maccatalyst-x64`, `maccatalyst-arm64` | ✅ | ✅ |
| iOS | device arm64 + simulator x64/arm64 | `ios-arm64`, `iossimulator-x64`, `iossimulator-arm64` | ✅ | ✅ |
| Android | arm (armeabi-v7a), arm64 (arm64-v8a), x64 (x86_64) | `android-arm`, `android-arm64`, `android-x64` | ✅ | ✅ |

> **VLC4 win-x86 not shipped:** VLC 4's `direct3d9` plugin links `DXVAHD_CreateDevice`,
> which is absent from llvm-mingw's 32-bit import libraries. D3D11 and all other
> modules remain available on 32-bit Windows via the `win-x64` package.

Consumable from **.NET MAUI**, **WinUI 3**, **Win32 / WPF / WinForms**,
**Avalonia**, and plain console apps on Linux/macOS/Windows.

## Enabled plugins (beyond VLC baseline)

The following plugins are enabled on top of the VLC default set:

| Plugin | Module | Platforms | Notes |
|--------|--------|-----------|-------|
| Audio fingerprint | `stream_out_chromaprint` | Win, Linux, macOS, Android | Built against bundled kissfft (no libavutil FFT) |
| x264 software H.264 encode | `x264` | Win, Linux, macOS, Catalyst, iOS/sim | — |
| x265 software H.265 encode | `x265` | Win, Linux, macOS, iOS-x64/sim-x64 | Not on arm64 iOS/sim/Catalyst (Xcode 16 asm tagging) |
| Blu-ray / DVD | `libbluray`, `dvdread`, `dvdnav` | Win, Linux, macOS, Android | — |
| taglib metadata | `taglib` | all RIDs | — |
| GnuTLS / SecureTransport TLS | `gnutls` / `securetransport` | all RIDs | HTTPS + native HLS/DASH decryption |
| SRT input + output | `access_srt`, `access_output_srt` | Win, Linux, macOS | — |
| RTSP (live555) | `live555` | all RIDs | — |

> The **`VLCDotNet3.Playback`** package drops the encoder plugins (`x264`,
> `x265`) and `stream_out_chromaprint` from this set; all decoders, demuxers and
> the network/TLS stack remain, so every format still plays.

## How the native binaries are built

All native runtimes are produced **entirely in GitHub Actions** — no binaries
are committed by hand. Each per-platform build:

1. Shallow-clones VideoLAN VLC at the pinned ref (a tag for 3.x, a branch/commit
   for the 4.x preview).
2. Applies the **unified-diff patches** in order (`git apply` →
   `patch -p1` fallback). Patches are kept in `patches/` per VLC series.
3. Restores the third-party dependency closure (the "contribs" — ffmpeg, codecs,
   demuxers, subtitle libs, …) built from source by the contrib workflows and
   stored as compressed, split archives in [`contribs/`](contribs).
4. `bootstrap` + `configure` for a **libvlc-only** build (no GUI player), then
   `make && make install` into a private prefix.
5. Normalizes the result into `artifacts/<rid>/` (`libvlc*`, `libvlccore*`,
   `plugins/`, licenses) — the stable layout the NuGet packer consumes. Apple
   mobile targets additionally link libvlc + libvlccore + plugins and a generated
   `vlc_static_modules[]` table into one signed, embedded dynamic
   `VLCDotNet.framework`.

### Full-source native builds

The work is split into two phases to fit the runners' disk and time budgets:

| Phase | Workflows | Result |
|-------|-----------|--------|
| **1 — Dependencies** | `contribs-vlc3`, `contribs-vlc4` | Build contribs per RID in chained `heavy` → `rest` jobs; compress, split into <100 MB parts, and commit to `contribs/vlc-{3,4}.0/<rid>/`. |
| **2 — Native runtime** | `build-vlc3`, `build-vlc4` | Restore archives and build libvlc, plugins and Apple mobile frameworks into `artifacts/<rid>/`. |

Contrib archives are reused until dependencies, patches or toolchains require
regeneration. See the [build guide](build/README.md) for commands and configuration.

## Repository layout

```
src/VLCDotNet/            Managed libvlc P/Invoke bindings (netstandard2.0 + mobile heads)
                         Also packs VLCDotNet3 / VLCDotNet4 with native binaries
PlaybackOnly/            Thin playback-only package (VLCDotNet3.Playback): reuses
                         src/VLCDotNet + build/, packs the encoder-free runtimes

build/                   Full-source native build scripts and documentation
patches/                 VLC 3 / VLC 4 unified-diff patches (per-series subdirectories)
contribs/                Committed, split contrib archives produced by CI

Tests/
  Data/vlc_test_pack/       Synthetic media test pack (video / audio / subtitles)
  VLCDotNet.Tests.Shared/   Shared test suite (video capture, audio probe, logging)
  VLCDotNet.Tests.Avalonia/ Desktop test app (Windows / Linux / macOS)
  VLCDotNet.Tests.Maui/     Mobile test app (iOS / Android / Mac Catalyst)

.github/workflows/       contribs / build / pack-release / test CI for VLC 3, VLC 4
                         and the playback-only variant
```

## Testing

Every produced NuGet is validated end-to-end in GitHub Actions against the
synthetic [`Tests/Data/vlc_test_pack`](Tests/Data/vlc_test_pack) media. All pack
files are exercised.

- **Desktop OSes** run the **Avalonia** app headless (`--autorun`); **mobile
  OSes** run the **MAUI** app. Both reference the packed NuGet and share one test
  suite ([`VLCDotNet.Tests.Shared`](Tests/VLCDotNet.Tests.Shared)).
- **Video** is verified by capturing frame snapshots via libvlc's vmem callbacks
  and asserting on the decoded image (brightness / color analysis).
- **Audio** is verified through the audio callbacks (channel/sample RMS probing).
- **Subtitles** (embedded + external SRT/VTT/ASS) and multi-track audio/subtitle
  switching are exercised.
- **VLC logging is enabled for every test** and written to log files; the logs
  are collected as CI artifacts.
- **iOS and Android run in the simulator/emulator** on the CI VMs.

### Recorded verification matrix — VLCDotNet3 3.0.24.1

| RID | libvlc linkage | Runtime-tested |
|---|---|---|
| `win-x64` | `libvlc.dll` (dynamic) | ✅ 55/55 tests |
| `win-x86` | `libvlc.dll` (dynamic) | cross-built + link-validated |
| `win-arm64` | `libvlc.dll` (dynamic) | cross-built |
| `linux-x64` | `libvlc.so` (dynamic) | ✅ 55/55 tests |
| `linux-x86` | `libvlc.so` (dynamic) | cross-built + link-validated |
| `linux-arm64`, `linux-arm` | `libvlc.so` (dynamic) | cross-built (libvlc via QEMU) |
| `osx-arm64` | `libvlc.dylib` (dynamic) | ✅ 54/55 (1 benign JPEG skip on macOS) |
| `osx-x64` | `libvlc.dylib` (dynamic) | cross-built |
| `android-x64` | `libvlc.so` (dynamic) | ✅ 26/26 tests (emulator) |
| `android-arm64`, `android-arm` | `libvlc.so` (dynamic) | cross-built |
| `iossimulator-arm64` | `VLCDotNet.framework` | ✅ pass (simulator) |
| `iossimulator-x64` | `VLCDotNet.framework` | cross-built |
| `ios-arm64` | `VLCDotNet.framework` | cross-built (no device runner) |
| `maccatalyst-arm64` | `VLCDotNet.framework` | ✅ pass (Catalyst) |
| `maccatalyst-x64` | `VLCDotNet.framework` | cross-built |

Recorded runtime limitations: hardware decode / HDR / zero-copy and physical
capture/disc require real devices. iOS device is cross-built since CI VMs have
no attached hardware.

## CI workflows

| Stage | VLC 3 | VLC 4 |
|-------|-------|-------|
| Build dependency archives | `contribs-vlc3` | `contribs-vlc4` |
| Build native runtimes | `build-vlc3` | `build-vlc4` |
| Pack / release NuGet | `pack-release-vlc3` | `pack-release-vlc4` |
| Test desktop consumers | `test-desktop-vlc3` | `test-desktop-vlc4` |
| Test mobile consumers | `test-mobile-vlc3` | `test-mobile-vlc4` |

Builds upload one `vlc3-native-<rid>` or `vlc4-native-<rid>` artifact per target.
The pack workflow validates that all required RIDs are present before producing
the NuGet; publication is controlled by the `publish_release` workflow input.

The **playback-only** package has its own parallel set of workflows, built with
`VLC_PROFILE=playback`: `contribs-playback-vlc3`, `build-playback-vlc3`,
`pack-release-playback-vlc3`, `test-desktop-playback-vlc3` and
`test-mobile-playback-vlc3`. They upload `vlc3play-native-<rid>` artifacts,
commit encoder-free contribs to `contribs/vlc-3.0-playback/<rid>/`, and release
under the `vplay3-*` tag.

## Building locally

The per-platform scripts can also run on a suitable host. For Linux x64 on
Ubuntu:

```sh
sudo bash build/deps-linux.sh
VLC_VERSION=3.0.24 bash build/build-linux.sh x86_64
ls artifacts/linux-x64
```

To regenerate the dependency archive first:

```sh
BUILD_PHASE=contrib bash build/build-linux.sh x86_64
```

See [`build/README.md`](build/README.md) for both phases and all targets.

## License

The repository-authored C# bindings and packaging code are licensed under the
**MIT License**; see [`LICENSE`](LICENSE).

Redistributed VLC binaries, plugins, and other third-party components keep their
upstream licenses. See [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) for the
applicable VLC/libvlc GPL/LGPL notices. Not affiliated with VideoLAN.
