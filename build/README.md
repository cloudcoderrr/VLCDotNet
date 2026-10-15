# Full-source native builds

This is the repository's native-build pipeline for **VLCDotNet3** and
**VLCDotNet4**, covering all 15 runtime IDs. VLC and its full contrib dependency
set are built from source in GitHub Actions with explicit platform and package
exclusions documented in [FEATURES.md](FEATURES.md).

Dependency builds and libvlc builds run in separate phases so they fit the
runners' disk and time budgets. Dependency archives are committed to the
repository and reused by later native-runtime builds.

## Build phases

| Phase | VLC 3 workflow | VLC 4 workflow | Result |
|-------|----------------|----------------|--------|
| Dependencies | `contribs-vlc3.yml` | `contribs-vlc4.yml` | Build contribs, compress with zstd, split into <100 MB parts, and commit to `contribs/vlc-<series>.0/<rid>/`. |
| Native runtime | `build-vlc3.yml` | `build-vlc4.yml` | Restore the matching contrib archive and build libvlc and plugins into `artifacts/<rid>/`. |

The `pack-release-vlc{3,4}.yml` workflows pack the managed bindings with all native
runtimes. The `test-desktop-vlc{3,4}.yml` and `test-mobile-vlc{3,4}.yml` workflows
exercise the same `vlc3-native-<rid>` / `vlc4-native-<rid>` artifacts through the
consumer apps.

Dispatch the contrib workflow for the selected family or RIDs before a runtime
build when its archives are absent or need regeneration. Rebuild contribs after
changes to their VLC source ref, dependency patches, feature selection or
toolchain. A runtime build requires an existing matching archive.

## Scripts and targets

| Script | Host / toolchain | Targets |
|--------|------------------|---------|
| `build-windows.sh <x86_64\|aarch64>` | Ubuntu / llvm-mingw | `win-x64`, `win-arm64` |
| `build-linux.sh <x86_64\|armv7\|aarch64>` | Ubuntu / GNU toolchains; ARM runtime builds under QEMU | `linux-x64`, `linux-arm`, `linux-arm64` |
| `build-apple.sh <macos\|ios\|iossimulator\|maccatalyst> <x86_64\|arm64>` | macOS / Xcode | `osx-*`, `ios-arm64`, `iossimulator-*`, `maccatalyst-*` |
| `build-android.sh <arm\|arm64\|x86_64>` | Ubuntu / Android NDK | `android-arm`, `android-arm64`, `android-x64` |

`BUILD_PHASE=contrib` selects dependency generation. The default phase is
`libvlc`, which restores contribs and builds the native runtime. Run commands
from the repository root on a host with the matching toolchain installed.

```sh
# Linux x64 on Ubuntu: install tools and system integration dependencies.
sudo bash build/deps-linux.sh

# Optional: regenerate the committed dependency archive from source.
BUILD_PHASE=contrib bash build/build-linux.sh x86_64
ls contribs/vlc-3.0/linux-x64

# Restore the archive and build libvlc (the default phase).
bash build/build-linux.sh x86_64
ls artifacts/linux-x64
```

CI sets `VLC_SERIES`, `VLC_VERSION` and, for VLC 4, `VLC_REF`. For a local VLC 4
build, supply the same values used by `build-vlc4.yml` and `contribs-vlc4.yml`.
The package coordinates and default upstream ref live in
[Directory.Build.props](../Directory.Build.props).

## Split dependency groups

CI builds each RID's contribs in chained jobs at identical absolute paths:

| Group | Make targets | Work |
|-------|--------------|------|
| `heavy` | `.ffmpeg .x265 .x264` | Build the largest dependencies. |
| `rest` | Default `make` target | Resume the saved state and build the remaining enabled packages. |

The first job retains the install prefix, package stamps and tarball cache,
reclaims extracted sources, and uploads `build/_state/<rid>/` as the
`contribstate-<rid>` workflow artifact. The next job restores that state and
packs the final dependency archive. See `build_contrib_group` in
[lib.sh](lib.sh). Local builds may leave `CONTRIB_GROUP` unset to build the set
in one invocation.

## Dependency archive layout

```text
contribs/vlc-<series>.0/<rid>/
  MANIFEST               # RID, triplet, checksum, original prefix, part count
  contrib.tar.zst.aaa
  contrib.tar.zst.aab     # additional parts as needed
```

Restoration reassembles the parts, verifies the SHA-256, extracts the contrib
prefix, and rewrites the original build path in `*.pc`, `*.la` and `*-config`
files. Existing manifests preserve their original `build_prefix`, even if it
uses a historical directory layout; that value is needed for relocation.

## Configuration

| Variable | Meaning |
|----------|---------|
| `BUILD_PHASE` | `contrib` to generate archives; `libvlc` to restore and build (default). |
| `VLC_VERSION` / `VLC_SERIES` / `VLC_REF` | Upstream version, major series and git ref; match the contrib and runtime phases. |
| `CONTRIB_GROUP` | Phase-1 group (`heavy` or `rest`); unset builds in one invocation. |
| `CONTRIB_GROUPS` | Ordered group list (default `heavy rest`). |
| `CONTRIB_DEFAULT_PRUNE` | Baseline contrib exclusions across all targets. |
| `CONTRIB_EXTRA_PRUNE` | Additional `--disable-<pkg>` flags for a target. |
| `VLC_EXCLUDED_NET_MODULES` | Optional space-separated module denylist; empty by default. |
| `CONTRIB_CACHE_DIR` | Dependency archive root (default `contribs/vlc-<series>.0`). |
| `CONTRIB_STATE_DIR` | Resumable group state root (default `build/_state`). |
| `CONTRIB_PART_SIZE` | Maximum archive part size (default 95,000,000 bytes). |
| `CONTRIB_ZSTD_LEVEL` | Compression level (default 19). |
| `WORK_DIR` / `ARTIFACTS_DIR` | Scratch and native-output roots (default `build/_work` / `artifacts`). |
| `ANDROID_NDK_HOME` / `ANDROID_API` | Android NDK directory and minimum API (default API 21). |

## Patches and output

Patches in [patches/vlc-3.0](../patches/vlc-3.0) or
[patches/vlc-4.0](../patches/vlc-4.0) are applied in lexical order before either
build phase (`git apply`, then `patch -p1` fallback). Android ARM additionally
applies patches from the selected series' `android-arm/` directory when present.

Desktop targets stage dynamic libvlc/libvlccore and plugins. Android stages
shared libraries, plugins and the NDK C++ runtime. iOS, simulator and Mac
Catalyst builds link their internal static archives and plugin registration
table into the embedded dynamic `VLCDotNet.framework` consumed by .NET apps.
All native outputs use `artifacts/<rid>/`, the layout consumed by NuGet packing.
