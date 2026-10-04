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
2. Applies the pipeline's **unified-diff patches** in order (`git apply` →
   `patch -p1` → 3-way). Patches are kept in unified format so the VLC source can
   be re-based onto a newer upstream release later; CI applies them with `git`.
3. Resolves the third-party dependency closure (the "contribs" — ffmpeg, codecs,
   demuxers, subtitle libs, …) using one of the three strategies below.
4. `bootstrap` + `configure` for a **libvlc-only** build (no GUI player), then
   `make && make install` into a private prefix.
5. Normalizes the result into `artifacts/<rid>/` (`libvlc*`, `libvlccore*`,
   `plugins/`, licenses) — the stable layout the NuGet packer consumes. Apple
   static targets (iOS/simulator/Mac Catalyst) additionally stage a
   `vlc_static_modules[]` table for static plugin registration.

### Native-build pipelines

The repo maintains **three independent native-build pipelines** (each a separate
set of scripts, patches, and workflows) that differ only in how step 3 — the
contrib dependency closure — is obtained:

| Pipeline | Dir | Contrib strategy |
|----------|-----|------------------|
| **From-source (minimal)** | [`build/`](build), [`patches/`](patches) | Builds a minimal, test-required contrib set from upstream source tarballs via VLC's contrib system on every run. |
| **VideoLAN prebuilt** | [`prebuilt/`](prebuilt) | Downloads VideoLAN's official `artifacts.videolan.org` prebuilt contrib bundles (distro `-dev` packages on Linux), with a source fallback. Toolchain-sensitive, so it only covers a subset of targets. |
| **CI-cached full contribs** | [`cached/`](cached) | Builds the **full** VideoLAN contrib set from source **in the GitHub Actions VMs**, commits the compressed result into the repo (split into <100 MB parts), and reuses it in the libvlc build. GitHub-Actions-compatible for all targets. |

The CI-cached pipeline exists because VideoLAN's published prebuilt bundles were
produced with toolchains that do not link against the GitHub-hosted runner
images for most targets. Building the contribs **inside** the runner images and
caching them in-repo makes every platform reproducible. Because a runner cannot
build all targets' contribs in one job (limited disk), the contrib build is
**split across multiple actions**: each action builds one target's contribs and
commits them; the libvlc build action then checks out the repo and consumes the
cached contribs. See [`cached/README.md`](cached/README.md).

## Repository layout

```
src/VLCDotNet/            Managed libvlc P/Invoke bindings (netstandard2.0 + mobile heads)
nuget/VLCDotNet3/         NuGet packing project (managed lib + all native binaries)

build/   + patches/       Pipeline 1 — from-source minimal contribs
prebuilt/                 Pipeline 2 — VideoLAN prebuilt contribs
cached/                   Pipeline 3 — CI-built full contribs cached in-repo
  cached/build/             per-platform build scripts
  cached/patches/           unified-diff patches
  cached/contribs/          committed, split contrib archives (produced by CI)

Tests/
  Data/vlc_test_pack/       Synthetic media test pack (video / audio / subtitles)
  VLCDotNet.Tests.Shared/   Shared test suite (video capture, audio probe, logging)
  VLCDotNet.Tests.Avalonia/ Desktop test app (Windows / Linux / macOS)
  VLCDotNet.Tests.Maui/     Mobile test app (iOS / Android / Mac Catalyst)

.github/workflows/        build / pack-release / test CI, one set per pipeline
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

When a test exposes a native defect, the fix is added as a **unified-diff patch**
to the relevant pipeline's `patches/` directory (and/or the test suite is
extended) and the build is re-run.

## CI workflows

Each pipeline has its own `build` → `pack-release` → `test` workflow trilogy in
[`.github/workflows`](.github/workflows), suffixed by pipeline and VLC series
(e.g. `build-vlc3`, `build-vlc3-prebuilt`, `build-vlc3-cached`). Builds upload
one artifact per RID; pack-release downloads them, packs the NuGet, and publishes
a GitHub Release; the test workflows consume the same artifacts.

## Building locally

The per-platform scripts can also run on a suitable host. Example (from-source
Linux x64 on Ubuntu with the build deps installed):

```sh
VLC_VERSION=3.0.23 build/build-linux.sh x86_64
ls artifacts/linux-x64
```

See each pipeline's `README` for details:
[`build/README.md`](build/README.md) · [`cached/README.md`](cached/README.md).

## License

The repository-authored C# bindings and packaging code are licensed under the
**MIT License**; see [`LICENSE`](LICENSE).

Redistributed VLC binaries, plugins, and other third-party components keep their
upstream licenses. See [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) for the
applicable VLC/libvlc GPL/LGPL notices. Not affiliated with VideoLAN.