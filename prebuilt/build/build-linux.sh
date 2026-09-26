#!/usr/bin/env bash
#
# Build libvlc 3.0.x for Linux using PREBUILT dependencies:
#   * the distribution's -dev packages (apt) provide the whole codec / demux /
#     subtitle dependency set (ffmpeg, faad2, flac, mad, mpcdec, theora, vpx,
#     ogg, opus, matroska, dvbpsi, ass, freetype, fontconfig, harfbuzz, ...);
#   * VLC's contrib system source-builds ONLY the two codecs current Ubuntu no
#     longer ships (schroedinger, sidplay2) so the test module set is complete.
#
# The resulting libvlc + plugins link the distro shared libraries, so we bundle
# the full non-system shared-object closure next to libvlc and rewrite rpaths to
# $ORIGIN, producing a relocatable runtime the NuGet can ship to any glibc-based
# consumer.
#
#   Usage: build-linux.sh <x86_64|armv7|aarch64>
#
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ARCH="${1:?arch required: x86_64 | armv7 | aarch64}"
case "${ARCH}" in
  x86_64)  RID="linux-x64";   TRIPLET="x86_64-linux-gnu" ;;
  armv7)   RID="linux-arm";   TRIPLET="arm-linux-gnueabihf" ;;
  aarch64) RID="linux-arm64"; TRIPLET="aarch64-linux-gnu" ;;
  *) die "Unsupported Linux arch: ${ARCH}" ;;
esac

# arm/arm64 build natively inside a QEMU-emulated container (see the workflow),
# so this script stays architecture-agnostic: it always does a native build
# against the distro packages and the two source-built niche codecs.

VLC_SRC="${WORK_DIR}/vlc-linux-${ARCH}"
INSTALL_PREFIX="${WORK_DIR}/install-linux-${ARCH}"
mkdir -p "${WORK_DIR}"

# sidplay2's legacy C++ trips narrowing diagnostics on modern GCC.
export CXXFLAGS="${CXXFLAGS:-} -Wno-narrowing"

clone_vlc "${VLC_SRC}"
apply_patches "${VLC_SRC}"

# ---- 1. niche codecs Ubuntu dropped: build via VLC contrib (source) ----------
# schroedinger (Dirac) and sidplay2 (SID) are no longer packaged by current
# Ubuntu, but the tests assert their VLC modules exist. Build just those two
# (plus their deps, e.g. orc) with the contrib system into contrib/<triplet>.
CONTRIB_BUILD="${VLC_SRC}/contrib/contrib-linux-${ARCH}"
mkdir -p "${CONTRIB_BUILD}"
(
  cd "${CONTRIB_BUILD}"
  ../bootstrap --disable-all --enable-schroedinger --enable-sidplay2
  make -j"$(jobs)" fetch
  make -j"$(jobs)" || make -j1
)
CONTRIB_PREFIX="${VLC_SRC}/contrib/${TRIPLET}"

# ---- 2. bootstrap + configure ------------------------------------------------
( cd "${VLC_SRC}" && ./bootstrap )

BUILD_DIR="${VLC_SRC}/build-${ARCH}"
rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}"

CONFIG_FLAGS=(
  "--prefix=${INSTALL_PREFIX}"
  "--with-contrib=${CONTRIB_PREFIX}"   # schroedinger + sidplay2 (source)
  --disable-vlc          # libvlc only, no player binary
  --disable-qt
  --disable-skins2
  --disable-nls
  --disable-lua
  --disable-a52
  --enable-avcodec       # FFmpeg-based decoders (apt)
  --enable-swscale
  --disable-vdpau
  --disable-sdl-image
  --disable-xcb          # headless: video verified via vmem callbacks
  --disable-alsa         # headless: audio verified via amem callbacks
  --disable-pulse
  --disable-bluray
  --disable-vnc
  --disable-gnutls
  --disable-srt
  --disable-chromaprint
  --enable-faad
  --enable-flac
  --enable-mad
  --enable-mpc
  --enable-schroedinger
  --enable-sid
  --enable-theora
  --enable-vpx
  --enable-shared
  --disable-static
)

(
  cd "${BUILD_DIR}"
  ../configure "${CONFIG_FLAGS[@]}"
  make -j"$(jobs)"
  make install
)

normalize_output "${RID}" "${INSTALL_PREFIX}"

# ---- 3. bundle the distro shared-object closure + rewrite rpaths --------------
bundle_linux_deps "${ARTIFACTS_DIR}/${RID}"

cp -a "${VLC_SRC}/COPYING"     "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
cp -a "${VLC_SRC}/COPYING.LIB" "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
