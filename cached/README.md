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

The build covers the full VLC contrib codec/demux/subtitle/**encoder**/disc/
metadata closure (ffmpeg, x264/x265, dav1d, aom-decode-via-dav1d, vpx, theora,
vorbis, opus, flac, speex, mpg123, faad2, mad, a52/dca, libass, freetype/
harfbuzz/fribidi, matroska, dvbpsi, taglib, dvdread/dvdnav/bluray, gme/modplug/
sidplay2/fluidsynth, ...) so the cached runtime reaches **main-media feature
parity** plus VLC's complete native transport/network stack (see
[FEATURE-RESTORATION-TASK.md](FEATURE-RESTORATION-TASK.md)).

A baseline prune (`CONTRIB_DEFAULT_PRUNE`) drops only host-owned or
genuinely-unbuildable NON-transport packages: **opencv/protobuf**
(computer vision), **chromaprint** (fingerprinting), **libplacebo/projectM/goom**
(GPU render + external visualizations), **Qt** (GUI), **medialibrary**,
**breakpad**, **xcb** (Linux links the *system* libxcb), the **x262** MPEG-2
encoder and **aom** AV1 encoder (AV1 decode is covered by dav1d) and **lua**
(scripting). The **network** group (gnutls, live555, librist, smb2, nfs,
microdns, upnp, srt, shout, ssh2) is now KEPT so the runtime ships the full
native transport stack; its crypto deps (nettle/gmp/gcrypt/gpg-error) build with
it. The encoders (x264/x265), the extra audio decoder (mpg123), the metadata
writer (taglib) and the LOCAL disc stack (dvdread/dvdnav/bluray + dvdcss) are
KEPT. Apple uses **securetransport** for TLS, so gnutls and its crypto deps are
pruned there. Override `CONTRIB_DEFAULT_PRUNE` to change the set.

### Split contrib groups

The full parity closure plus a libvlc build will not co-reside within a single
runner's disk/time budget, so **Phase 1 is itself split into ordered GROUPS**,
one CI job each (`CONTRIB_GROUP`, default order `heavy rest`):

| Group | `make` targets | What it builds |
|-------|----------------|----------------|
| `heavy` | `.ffmpeg .x265 .x264` | the long poles, isolated |
| `rest`  | bare `make` | every other enabled package |

The `heavy` job bootstraps + fetches, builds the long poles, reclaims the
extracted source trees (keeping only the install prefix + `.<pkg>` stamp files +
tarball cache) and uploads that resumable state as the `contribstate-<rid>`
workflow artifact. The `rest` job downloads it, so VLC contrib skips every
already-stamped package and bare-`make`s only the remainder, then packs the
committed cache. Both group jobs for one RID run on the same runner OS at
identical absolute paths within one workflow run, so the state restores verbatim
with no prefix rewrite. See `build_contrib_group` in [`build/lib.sh`](build/lib.sh).


## Two phases

A full contrib closure plus a libvlc build will not co-reside within a single
runner's disk budget, so the work is split into two actions wired through the
repository:

| Phase | Workflow | What it does |
|-------|----------|--------------|
| **1 — contrib** | `contribs-vlc3-cached.yml` | For each RID, in chained `heavy` → `rest` group jobs (see above): build the full parity contrib set, compress it (`zstd`), split into <100 MB parts, and **commit** them to `cached/contribs/vlc-3.0/<rid>/`. |
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
| `CONTRIB_GROUP` | Which Phase-1 split group to build (`heavy` or `rest`); unset = legacy single-shot full build. |
| `CONTRIB_GROUPS` | Ordered group list the chain walks (default `heavy rest`; e.g. `ffmpeg x265 x264 rest` for a finer split). |
| `VLC_VERSION` / `VLC_SERIES` / `VLC_REF` | VLC version / series / git ref (defaults from `Directory.Build.props`). |
| `CONTRIB_DEFAULT_PRUNE` | Baseline contrib bootstrap prune applied on every target (drops non-transport specialist packages; keeps the network group so the native transport stack ships). |
| `VLC_EXCLUDED_NET_MODULES` | Optional space-separated module denylist stripped from the staged runtime / Apple static table (empty by default: ship everything). |
| `CACHED_CONTRIB_PRUNE` | Extra `--disable-<pkg>` bootstrap flags to drop packages that cannot cross-build for a target. |
| `CONTRIB_PART_SIZE` | Max committed part size in bytes (default 95 MB; GitHub rejects files >100 MiB). |
| `CONTRIB_ZSTD_LEVEL` | zstd compression level for the cache (default 19). |
| `WORK_DIR` / `ARTIFACTS_DIR` | Scratch dir / output root. |

## Patches

Unified-diff patches in [`patches/vlc-3.0/`](patches/vlc-3.0) (and `vlc-4.0/`)
are applied to the VLC source before building, in lexical order
(`git apply` → `patch -p1`). 32-bit Android also applies `patches/vlc-3.0/android-arm/*`.
Add new fixes here in unified format so the VLC source can be re-based later.
