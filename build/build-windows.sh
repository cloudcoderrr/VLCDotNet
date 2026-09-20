#!/usr/bin/env bash
#
# Build libvlc for Windows (x64 / arm64) by cross-compiling on Ubuntu with the
# official VideoLAN win32 build script. Prebuilt contribs are used to keep the
# job within CI time limits, falling back to a source contrib build.
#
#   Usage: build-windows.sh <x86_64|aarch64>
#
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ARCH="${1:?arch required: x86_64 | aarch64}"
case "${ARCH}" in
  x86_64)  RID="win-x64"   ;;
  aarch64) RID="win-arm64" ;;
  *) die "Unsupported Windows arch: ${ARCH} (expected x86_64 or aarch64)" ;;
esac

VLC_SRC="${WORK_DIR}/vlc-win-${ARCH}"
mkdir -p "${WORK_DIR}"

clone_vlc "${VLC_SRC}"
apply_patches "${VLC_SRC}"

# Use llvm-mingw (UCRT, up-to-date Windows SDK headers) for every arch. The apt
# mingw-w64 headers are too old for VLC's d3d11 output (missing newer DXGI types),
# and only llvm-mingw provides an aarch64 target.
LLVM_MINGW_VER="${LLVM_MINGW_VER:-20240619}"
LLVM_MINGW_DIR="${LLVM_MINGW_DIR:-/opt/llvm-mingw}"
if [ ! -x "${LLVM_MINGW_DIR}/bin/${ARCH}-w64-mingw32-clang" ]; then
  tarball="llvm-mingw-${LLVM_MINGW_VER}-ucrt-ubuntu-20.04-x86_64.tar.xz"
  log "Installing llvm-mingw ${LLVM_MINGW_VER}"
  curl -fSL "https://github.com/mstorsjo/llvm-mingw/releases/download/${LLVM_MINGW_VER}/${tarball}" -o /tmp/llvm-mingw.tar.xz
  sudo mkdir -p "${LLVM_MINGW_DIR}"
  sudo tar -xf /tmp/llvm-mingw.tar.xz -C "${LLVM_MINGW_DIR}" --strip-components=1
fi
export PATH="${LLVM_MINGW_DIR}/bin:${PATH}"

# -u: llvm-mingw is UCRT-only. -S 0x0A000000: target the Windows 10 API level so
# VLC's d3d11 output can use Win8+ DXGI types (IID_IDXGIResource1, etc.).
BUILD_ARGS=(-r -z -u -S 0x0A000000 -a "${ARCH}")

# The GitHub runner currently cannot reach VideoLAN's prebuilt contrib host
# reliably on either Windows arch, and the x86_64 bundle also mismatches our
# UCRT llvm-mingw toolchain. Build contribs from source for both arches so the
# inputs are consistent and all package fetches go through our patched source
# URLs instead of the prebuilt archive host.

# libbluray is optional for our package and currently fails to link on the
# x86_64 llvm-mingw UCRT path because the contrib freetype archive pulls an
# unresolved _setjmp into liblibbluray_plugin.la. Disable the VLC bluray module
# at configure time so both Windows arches build a consistent plugin set.
export CONFIGFLAGS="${CONFIGFLAGS:-} --disable-bluray"
export CONTRIBFLAGS="${CONTRIBFLAGS:-} --disable-a52 --disable-dca"
EXTRA_LINK_FLAGS=""
if [ "${ARCH}" = "x86_64" ]; then
  # x86_64 UCRT contribs such as freetype and SDL_image emit references to
  # _setjmp. Resolve them through mingw-w64's support library at cross-link
  # time only; leaking this into build.sh's native tools breaks configure.
  EXTRA_LINK_FLAGS="-lmingwex"
fi

# Install directory that the win32 build script populates via package-win-install.
INSTALL_PREFIX="${WORK_DIR}/install-win-${ARCH}"
rm -rf "${INSTALL_PREFIX}"
mkdir -p "${INSTALL_PREFIX}"
BUILD_ARGS+=(-o "${INSTALL_PREFIX}")

# VLC 3.0.23 C code trips clang default-error diagnostics (e.g. the obsolete
# crystalhd decoder). Wrap ONLY the cross compilers to downgrade them; exporting
# CFLAGS globally would leak clang-only flags into build.sh's native tools (gcc)
# build and break it ("C compiler cannot create executables").
WRAP_DIR="${WORK_DIR}/xwrap-${ARCH}"
mkdir -p "${WRAP_DIR}"
LENIENT="-Wno-error=incompatible-function-pointer-types -Wno-error=incompatible-pointer-types -Wno-error=implicit-function-declaration -Wno-error=int-conversion"
# Map the gcc/g++ names build.sh invokes to llvm-mingw clang/clang++ (current
# Windows SDK headers) and add the leniency flags. Only the cross compilers are
# affected; native gcc used for build.sh's tools is untouched.
for pair in "gcc:clang" "g++:clang++" "clang:clang" "clang++:clang++" ; do
  name="${pair%%:*}"; realt="${pair##*:}"
  cat > "${WRAP_DIR}/${ARCH}-w64-mingw32-${name}" <<EOF
#!/bin/sh
extra_link_flags=""
for arg in "\$@"; do
  case "\$arg" in
    -c|-E|-S)
      exec "${LLVM_MINGW_DIR}/bin/${ARCH}-w64-mingw32-${realt}" ${LENIENT} "\$@"
      ;;
  esac
done
if [ -n "${EXTRA_LINK_FLAGS}" ]; then
  # Only x86_64 uses EXTRA_LINK_FLAGS, and these wrappers are only used for
  # target cross-compiler invocations. Apply the support library on every link
  # step so libtool output naming does not decide whether _setjmp resolves.
  extra_link_flags="${EXTRA_LINK_FLAGS}"
fi
exec "${LLVM_MINGW_DIR}/bin/${ARCH}-w64-mingw32-${realt}" ${LENIENT} "\$@" \$extra_link_flags
EOF
  chmod +x "${WRAP_DIR}/${ARCH}-w64-mingw32-${name}"
done
export PATH="${WRAP_DIR}:${PATH}"

# We only need libvlc + plugins, not the NSIS installer. Provide a no-op makensis
# so the win32 build script's package step completes without building an installer.
sudo tee /usr/local/bin/makensis >/dev/null <<'EOF'
#!/bin/sh
exit 0
EOF
sudo chmod +x /usr/local/bin/makensis

log "Running extras/package/win32/build.sh ${BUILD_ARGS[*]}"
(
  cd "${VLC_SRC}"
  ./extras/package/win32/build.sh "${BUILD_ARGS[@]}"
)

normalize_output "${RID}" "${INSTALL_PREFIX}"
