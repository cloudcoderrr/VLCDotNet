# Cached-contrib pipeline (pipeline 3)

Builds libvlc from VideoLAN source against the **full** VLC third-party
dependency closure ("contribs"), where the contribs are **built from source
inside the GitHub Actions runner images** and **cached in this repository**.

This exists because VideoLAN's published prebuilt contrib bundles
(`artifacts.videolan.org`) were produced with toolchains that do not link
against the GitHub-hosted runner images for most targets. Building the contribs
*in* the runner images and caching them makes every one of the 15 runtime IDs
reproducible with a GitHub-Actions-compatible toolchain.

### Contrib scope

The build covers the full VLC contrib codec/demux/subtitle closure (ffmpeg,
x264/x265, dav1d, aom, vpx, theora, vorbis, opus, flac, faad2, mad, libass,
freetype/harfbuzz/fribidi, matroska, dvbpsi, ...). A baseline prune
(`CONTRIB_DEFAULT_PRUNE`) drops packages that a libvlc **file-playback** runtime
does not use and that fail / are fragile on the GitHub runners: the **disc** and
**network** groups (cddb, libcdio, dvd*, bluray, gnutls, srt, live555, smb2, ...),
**mpg123** (MP3 is decoded by ffmpeg), **opencv/protobuf** (video analysis),
**chromaprint**, **libplacebo/projectM/goom** (GPU render + visualizations),
**Qt** (GUI), **medialibrary**, and **breakpad**. The full
codec/demux/subtitle/audio closure is kept. Override the knob to change the set.

## Two phases

A full contrib closure plus a libvlc build will not co-reside within a single
runner's disk budget, so the work is split into two actions wired through the
repository:

| Phase | Workflow | What it does |
|-------|----------|--------------|
| **1 — contrib** | `contribs-vlc3-cached.yml` | For each RID: build the full contrib set, compress it (`zstd`), split into <100 MB parts, and **commit** them to `cached/contribs/vlc-3.0/<rid>/`. |
| **2 — libvlc** | `build-vlc3-cached.yml` | Check out the repo (now containing the cached contribs), **restore** the matching contrib prefix, and link libvlc against it → `artifacts/<rid>`. |

`pack-release-vlc3-cached.yml` then packs the `VLCDotNet3` NuGet from the Phase 2
artifacts, and `test-desktop-vlc3-cached.yml` / `test-mobile-vlc3-cached.yml`
run the shared test suite against them.

## Scripts

| Script | Runner | Targets |
|--------|--------|---------|
| `build/build-windows.sh <x86_64\|aarch64>` | Ubuntu (llvm-mingw cross) | `win-x64`, `win-arm64` |
| `build/build-linux.sh <x86_64\|armv7\|aarch64>` | Ubuntu (native + QEMU for arm) | `linux-x64`, `linux-arm`, `linux-arm64` |
| `build/build-apple.sh <macos\|ios\|iossimulator\|maccatalyst> <x86_64\|arm64>` | macOS (Xcode) | `osx-*`, `ios-*`, `iossimulator-*`, `maccatalyst-*` |
| `build/build-android.sh <arm\|arm64\|x86_64>` | Ubuntu (Android NDK) | `android-arm`, `android-arm64`, `android-x64` |

Each script runs **both** phases, selected by the `CACHED_PHASE` env var:

```sh
# Phase 1: build the full contrib set and cache it in-repo.
CACHED_PHASE=contrib cached/build/build-linux.sh x86_64
ls cached/contribs/vlc-3.0/linux-x64         # MANIFEST + contrib.tar.zst.*

# Phase 2 (default): restore the cached contrib set and build libvlc.
cached/build/build-linux.sh x86_64
ls artifacts/linux-x64                        # libvlc*, plugins/, licenses
```

The shared helpers live in [`build/lib.sh`](build/lib.sh):
`build_full_contrib`, `pack_contrib`, `restore_contrib`.

## Contrib cache layout

```
cached/contribs/vlc-3.0/<rid>/
  MANIFEST              # rid, triplet, sha256, origin build_prefix, part count, ...
  contrib.tar.zst.aaa  # split parts of the zstd-compressed contrib/<triplet> prefix
  contrib.tar.zst.aab
  ...
```

Restore reassembles the parts (`cat contrib.tar.zst.*`), verifies the SHA-256,
extracts into `contrib/<triplet>`, and rewrites the recorded absolute build
prefix (in `*.pc` / `*.la` / `*-config`) to the current location.

## Environment knobs

| Variable | Meaning |
|----------|---------|
| `CACHED_PHASE` | `contrib` (build + cache) or `libvlc` (restore + build, default). |
| `VLC_VERSION` / `VLC_SERIES` / `VLC_REF` | VLC version / series / git ref (defaults from `Directory.Build.props`). |
| `CONTRIB_DEFAULT_PRUNE` | Baseline contrib bootstrap prune applied on every target (default `--disable-disc --disable-net --disable-mpg123`). |
| `CACHED_CONTRIB_PRUNE` | Extra `--disable-<pkg>` bootstrap flags to drop packages that cannot cross-build for a target. |
| `CONTRIB_PART_SIZE` | Max committed part size in bytes (default 95 MB; GitHub rejects files >100 MiB). |
| `CONTRIB_ZSTD_LEVEL` | zstd compression level for the cache (default 19). |
| `WORK_DIR` / `ARTIFACTS_DIR` | Scratch dir / output root. |

## Patches

Unified-diff patches in [`patches/vlc-3.0/`](patches/vlc-3.0) (and `vlc-4.0/`)
are applied to the VLC source before building, in lexical order
(`git apply` → `patch -p1`). 32-bit Android also applies `patches/vlc-3.0/android-arm/*`.
Add new fixes here in unified format so the VLC source can be re-based later.
