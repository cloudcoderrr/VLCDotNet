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

# -u: llvm-mingw is UCRT-only.
BUILD_ARGS=(-r -z -p -u -a "${ARCH}")

# Install directory that the win32 build script populates via package-win-install.
INSTALL_PREFIX="${WORK_DIR}/install-win-${ARCH}"
rm -rf "${INSTALL_PREFIX}"
mkdir -p "${INSTALL_PREFIX}"
BUILD_ARGS+=(-o "${INSTALL_PREFIX}")

log "Running extras/package/win32/build.sh ${BUILD_ARGS[*]}"
(
  cd "${VLC_SRC}"
  ./extras/package/win32/build.sh "${BUILD_ARGS[@]}"
)

normalize_output "${RID}" "${INSTALL_PREFIX}"
