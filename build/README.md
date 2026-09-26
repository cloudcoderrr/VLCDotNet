# Building libvlc from source

All native binaries in `VLCDotNet3` are built from VideoLAN source in CI by the
[`build-vlc3`](../.github/workflows/build-vlc3.yml) workflow. The per-platform
logic lives in this folder so it can also be run locally on a suitable host.

| Script | Runner | Targets (RID) |
|--------|--------|---------------|
| `build-windows.sh <x86_64\|aarch64>` | Ubuntu (mingw / llvm-mingw cross) | `win-x64`, `win-arm64` |
| `build-linux.sh <x86_64\|armv7\|aarch64>` | Ubuntu (native + GNU cross) | `linux-x64`, `linux-arm`, `linux-arm64` |
| `build-apple.sh <macos\|ios\|iossimulator\|maccatalyst> <x86_64\|arm64>` | macOS (Xcode) | `osx-*`, `ios-*`, `iossimulator-*`, `maccatalyst-*` |
| `build-android.sh <arm\|arm64\|x86_64>` | Ubuntu (Android NDK) | `android-arm`, `android-arm64`, `android-x64` |

## Flow

Every script performs the same high-level steps (`build/lib.sh`):

1. Shallow-clone VLC at the tag in `VLC_VERSION` (defaults to `Directory.Build.props`'s `VlcVersion`).
2. Apply every `patches/vlc-3.0/*.patch` (unified diff, `git apply` → `patch -p1` → 3-way).
3. Build the shared minimal third-party dependency closure with the VLC **contrib**
   system (see `minimal_local_playback_contrib_flags` in `build/lib.sh`).
4. `./bootstrap`, then `configure` for a **libvlc-only** build (`--disable-vlc`, no GUI).
5. `make && make install` into a private prefix.
6. Normalize the result into `artifacts/<rid>/` (`libvlc*`, `libvlccore*`, `plugins/`, licenses).

## Environment knobs

| Variable | Meaning |
|----------|---------|
| `VLC_VERSION` | VLC git tag to build (default `3.0.23`). |
| `WORK_DIR` | Scratch checkout/build dir (default `build/_work`). |
| `ARTIFACTS_DIR` | Output root (default `artifacts`). |
| `ANDROID_NDK_HOME` | Android NDK path (Android only). |

## Local example

```sh
# Linux x64, on an Ubuntu box with the build deps installed:
VLC_VERSION=3.0.23 build/build-linux.sh x86_64
ls artifacts/linux-x64
```
