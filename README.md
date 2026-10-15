# VLCDotNet

Production-ready, cross-platform **libvlc** P/Invoke bindings for .NET, packaged
with native VLC runtimes **built from VideoLAN source in CI** for every supported
target. The managed bindings are MIT-licensed; the redistributed VLC binaries
keep their upstream GPL/LGPL licenses (see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)).
Not affiliated with VideoLAN.

## Packages

Two NuGet packages are produced and published on the repository's
[GitHub Releases](https://github.com/cloudcoderrr/VLCDotNet/releases) page:

| Package | libvlc | Status |
|---------|--------|--------|
| `VLCDotNet3` | VLC **3.0.x** (stable) | primary |
| `VLCDotNet4` | VLC **4.0.x** (preview) | ships after v3 |

The targeted VLC versions live in [`Directory.Build.props`](Directory.Build.props)
(`VlcVersion` / `VlcVersion4` + `VlcRef4`).

## Supported platforms

Native binaries are shipped for **6 OS families / 15 runtime IDs**, covering
every architecture below. WinUI 3 and Win32/WPF/WinForms consume the same
`win-x64` / `win-arm64` runtimes.

| OS | Architectures | RIDs |
|----|---------------|------|
| Windows | x64, arm64 | `win-x64`, `win-arm64` |
| Linux | x64, arm, arm64 | `linux-x64`, `linux-arm`, `linux-arm64` |
| macOS | x64, arm64 | `osx-x64`, `osx-arm64` |
| Mac Catalyst | x64, arm64 | `maccatalyst-x64`, `maccatalyst-arm64` |
| iOS | device arm64 + simulator x64/arm64 | `ios-arm64`, `iossimulator-x64`, `iossimulator-arm64` |
| Android | arm (armeabi-v7a), arm64 (arm64-v8a), x64 (x86_64) incl. emulator | `android-arm`, `android-arm64`, `android-x64` |

Consumable from **.NET MAUI**, **WinUI 3**, **Win32 / WPF / WinForms**,
**Avalonia**, and plain console apps on Linux/macOS/Windows.

## How the native binaries are built

All native runtimes are produced **entirely in GitHub Actions** — no binaries
are committed by hand. Each per-platform build:

1. Shallow-clones VideoLAN VLC at the pinned ref (a tag for 3.x, a branch/commit
   for the 4.x preview).
2. Applies the **unified-diff patches** in order (`git apply` →
   `patch -p1` fallback). Patches are kept in unified format so the VLC source can
   be re-based onto a newer upstream release later; CI applies them with `git`.
3. Restores the third-party dependency closure (the "contribs" — ffmpeg, codecs,
   demuxers, subtitle libs, …) built from source by the contrib workflows and
   stored as compressed, split archives in [`contribs/`](contribs).
4. `bootstrap` + `configure` for a **libvlc-only** build (no GUI player), then
   `make && make install` into a private prefix.
5. Normalizes the result into `artifacts/<rid>/` (`libvlc*`, `libvlccore*`,
   `plugins/`, licenses) — the stable layout the NuGet packer consumes. Apple
   mobile targets (iOS/simulator/Mac Catalyst) additionally link libvlc +
   libvlccore + the selected plugins and a generated `vlc_static_modules[]` registration
   table into one signed, embedded dynamic `VLCDotNet.framework`, so consuming
   apps link libvlc **dynamically** (no app-linked archives, no libvlc
   `__Internal`).

### Full-source native builds

The repository uses one native-build pipeline for VLC 3 and VLC 4 across all
15 RIDs. VLC and its contrib dependencies are built from source inside the
GitHub Actions runner images, using compatible toolchains. The full VLC contrib
set is enabled with explicit exclusions for unsupported or specialist packages;
see [feature scope and platform exclusions](build/FEATURES.md).

The work is split into two phases to fit the runners' disk and time budgets:

| Phase | Workflows | Result |
|-------|-----------|--------|
| **1 — Dependencies** | `contribs-vlc3`, `contribs-vlc4` | Build contribs per RID in chained `heavy` → `rest` jobs; compress, split into <100 MB parts, and commit the archives to `contribs/vlc-{3,4}.0/<rid>/`. |
| **2 — Native runtime** | `build-vlc3`, `build-vlc4` | Restore those archives and build libvlc, plugins and Apple mobile frameworks into `artifacts/<rid>/`. |

Contrib archives are reused until dependencies, patches or toolchains require
regeneration. Run the contrib workflow first for any RID without a matching
cache. See the [build guide](build/README.md) for commands and configuration.

## Repository layout

```
src/VLCDotNet/            Managed libvlc P/Invoke bindings (netstandard2.0 + mobile heads)
                         Also packs VLCDotNet3 / VLCDotNet4 with native binaries

build/                   Full-source native build scripts and documentation
patches/                 VLC 3 / VLC 4 unified-diff patches
contribs/                Committed, split contrib archives produced by CI

Tests/
  Data/vlc_test_pack/       Synthetic media test pack (video / audio / subtitles)
  VLCDotNet.Tests.Shared/   Shared test suite (video capture, audio probe, logging)
  VLCDotNet.Tests.Avalonia/ Desktop test app (Windows / Linux / macOS)
  VLCDotNet.Tests.Maui/     Mobile test app (iOS / Android / Mac Catalyst)

.github/workflows/       contribs / build / pack-release / test CI for VLC 3 and 4
```

## Testing

Every produced NuGet is validated end-to-end in GitHub Actions against the
synthetic [`Tests/Data/vlc_test_pack`](Tests/Data/vlc_test_pack) media (locally
generated video, audio, and subtitle files — see its
[README](Tests/Data/vlc_test_pack/README.txt)). All pack files are exercised.

- **Desktop OSes** run the **Avalonia** app headless (`--autorun`); **mobile
  OSes** run the **MAUI** app. Both reference the packed NuGet and share one test
  suite ([`VLCDotNet.Tests.Shared`](Tests/VLCDotNet.Tests.Shared)).
- **Video** is verified by capturing frame snapshots via libvlc's video
  callbacks and asserting on the decoded image (color/coverage analysis).
- **Audio** is verified through the audio callbacks (channel/sample probing).
- **Subtitles** (embedded + external SRT/VTT/ASS) and multi-track audio/subtitle
  switching are exercised.
- **VLC logging is enabled for every test** and written to log files; the logs
  are collected as CI artifacts and verified.
- **iOS and Android run in the simulator/emulator** on the CI VMs.
- On completion the VLC logs, frame snapshots, and audio snippets are downloaded
  and verified.

### Recorded verification matrix

Both production NuGets — **VLCDotNet3** (libvlc `3.0.23`) and **VLCDotNet4**
(libvlc 4.0 `e77e49b5`) — build for all 15 RIDs and link libvlc **dynamically**
everywhere (no app-linked archives, no libvlc `__Internal`): desktop resolves
`libvlc.{dll,so,dylib}` by name, Android ships `libvlc.so` as an
`AndroidNativeLibrary`, and Apple mobile embeds the signed, dynamic
`VLCDotNet.framework`. RIDs with a matching CI runner / emulator / simulator are
additionally exercised **end-to-end at runtime** through the packaged consumer
apps (instance creation, module registration, video decode + snapshots, audio
RMS, subtitles, metadata, encode); the rest are cross-built and link-validated.

| RID | libvlc linkage | Runtime-tested in CI |
|---|---|---|
| `win-x64` | `libvlc.dll` (dynamic) | ✅ VLC3 + VLC4 |
| `win-arm64` | `libvlc.dll` (dynamic) | cross-built |
| `linux-x64` | `libvlc.so` (dynamic) | ✅ VLC3 + VLC4 |
| `linux-arm64`, `linux-arm` | `libvlc.so` (dynamic) | cross-built (arm libvlc via QEMU) |
| `osx-arm64` | `libvlc.dylib` (dynamic) | ✅ VLC3 |
| `osx-x64` | `libvlc.dylib` (dynamic) | cross-built |
| `android-x64` | `libvlc.so` (dynamic) | ✅ VLC3 + VLC4 (emulator) |
| `android-arm64`, `android-arm` | `libvlc.so` (dynamic) | cross-built |
| `iossimulator-arm64` | `VLCDotNet.framework` (embedded dynamic) | ✅ VLC3 + VLC4 |
| `iossimulator-x64` | `VLCDotNet.framework` (embedded dynamic) | cross-built |
| `ios-arm64` | `VLCDotNet.framework` (embedded dynamic) | cross-built (no device in CI) |
| `maccatalyst-arm64` | `VLCDotNet.framework` (embedded dynamic) | ✅ VLC3 + VLC4 |
| `maccatalyst-x64` | `VLCDotNet.framework` (embedded dynamic) | cross-built |

Recorded runtime limitations: hardware decode / HDR / zero-copy and physical
capture/disc require real devices (CI simulators fall back to software decode);
iOS device and the non-host CPU slices are cross-built and link-validated since
the CI VMs have no matching attached hardware.

When a test exposes a native defect, the fix is added as a **unified-diff patch**
to the matching VLC series in `patches/` (and/or the test suite is
extended) and the build is re-run.

## CI workflows

The workflows in [`.github/workflows`](.github/workflows) cover both VLC series:

| Stage | VLC 3 | VLC 4 |
|-------|-------|-------|
| Build dependency archives | `contribs-vlc3` | `contribs-vlc4` |
| Build native runtimes | `build-vlc3` | `build-vlc4` |
| Pack / release NuGet | `pack-release-vlc3` | `pack-release-vlc4` |
| Test desktop consumers | `test-desktop-vlc3` | `test-desktop-vlc4` |
| Test mobile consumers | `test-mobile-vlc3` | `test-mobile-vlc4` |

Builds upload one `vlc3-native-<rid>` or `vlc4-native-<rid>` artifact per target. The pack and
test workflows consume those artifacts. Packing validates that all 15 RIDs are
present before producing the NuGet; publication is controlled by the
`publish_release` workflow input.

## Building locally

The per-platform scripts can also run on a suitable host. For Linux x64 on
Ubuntu, install the dependencies and restore the committed contrib archive:

```sh
sudo bash build/deps-linux.sh
VLC_VERSION=3.0.23 bash build/build-linux.sh x86_64
ls artifacts/linux-x64
```

To regenerate the dependency archive first, run
`BUILD_PHASE=contrib bash build/build-linux.sh x86_64` on a suitable host.
See [`build/README.md`](build/README.md) for both phases and all targets.

## License

The repository-authored C# bindings and packaging code are licensed under the
**MIT License**; see [`LICENSE`](LICENSE).

Redistributed VLC binaries, plugins, and other third-party components keep their
upstream licenses. See [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) for the
applicable VLC/libvlc GPL/LGPL notices. Not affiliated with VideoLAN.
