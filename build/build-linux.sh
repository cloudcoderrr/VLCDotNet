#!/usr/bin/env bash
#
# Build libvlc for Linux (x64 / arm / arm64) using the VLC contrib system and a
# libvlc-only configure. x64 builds natively; arm/arm64 cross-compile with the
# GNU cross toolchains. Prebuilt contribs are used when available.
#
#   Usage: build-linux.sh <x86_64|armv7|aarch64>
#
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ARCH="${1:?arch required: x86_64 | armv7 | aarch64}"
case "${ARCH}" in
  x86_64)  RID="linux-x64";   TRIPLET="x86_64-linux-gnu";      CROSS=0 ;;
  armv7)   RID="linux-arm";   TRIPLET="arm-linux-gnueabihf";   CROSS=1 ;;
  aarch64) RID="linux-arm64"; TRIPLET="aarch64-linux-gnu";     CROSS=1 ;;
  *) die "Unsupported Linux arch: ${ARCH}" ;;
esac

VLC_SRC="${WORK_DIR}/vlc-linux-${ARCH}"
INSTALL_PREFIX="${WORK_DIR}/install-linux-${ARCH}"
mkdir -p "${WORK_DIR}"

clone_vlc "${VLC_SRC}"
apply_patches "${VLC_SRC}"

if [ "${CROSS}" = "1" ]; then
  export CC="${TRIPLET}-gcc"
  export CXX="${TRIPLET}-g++"
  export LD="${TRIPLET}-gcc"
  export AR="${TRIPLET}-ar"
  export RANLIB="${TRIPLET}-ranlib"
  export STRIP="${TRIPLET}-strip"
  export PKG_CONFIG_LIBDIR="/usr/lib/${TRIPLET}/pkgconfig:/usr/${TRIPLET}/lib/pkgconfig"
fi

# ---- 1. contribs --------------------------------------------------------------
CONTRIB_BUILD="${VLC_SRC}/contrib/contrib-${ARCH}"
mkdir -p "${CONTRIB_BUILD}"
(
  cd "${CONTRIB_BUILD}"
  BOOT_ARGS=(--disable-gnutls --disable-x264 --disable-x265 --disable-mpg123 --disable-protobuf --disable-xcb --disable-sidplay2)
  if [ "${CROSS}" = "1" ]; then
    BOOT_ARGS+=(--disable-SDL_image)
  fi
  if [ "${ARCH}" = "armv7" ]; then
    # GCC 13 trips aom's ARM NEON contrib sources on this cross target; libvlc
    # still has AV1 decode coverage through --enable-avcodec.
    BOOT_ARGS+=(--disable-aom)
  fi
  if [ "${CROSS}" = "1" ]; then
    # Explicit --build is required so autoconf treats this as a cross build and
    # never tries to execute target binaries on the x86_64 runner.
    BOOT_ARGS+=(--host="${TRIPLET}" --build="x86_64-linux-gnu")
  fi
  # VLC is a GPL project; allow GPL contribs (freetype2 et al. gate on it) but
  # skip the slow encoders we never need for decode, and gnutls (local files only).
  ../bootstrap "${BOOT_ARGS[@]}"
  log "Fetching prebuilt contribs (fallback to source build)"
  if ! make prebuilt 2>/dev/null; then
    warn "Prebuilt contribs unavailable for ${TRIPLET}; building from source"
    make -j"$(jobs)" fetch
    make -j"$(jobs)" || make -j1
  fi
  make .luac || true
)

# ---- 2. bootstrap + configure -------------------------------------------------
# gnutls in the 3.0 contrib links a bundled nettle backport that leaves undefined
# symbols in dependent plugins (vnc/srt). We only play local files, so remove
# gnutls/srt from the contrib prefix to keep those modules out of the build.
CONTRIB_PREFIX_DIR="${VLC_SRC}/contrib/${TRIPLET}"
rm -f "${CONTRIB_PREFIX_DIR}"/lib/libgnutls* \
      "${CONTRIB_PREFIX_DIR}"/lib/pkgconfig/gnutls.pc \
      "${CONTRIB_PREFIX_DIR}"/lib/libsrt* \
      "${CONTRIB_PREFIX_DIR}"/lib/pkgconfig/srt.pc 2>/dev/null || true

( cd "${VLC_SRC}" && ./bootstrap )

BUILD_DIR="${VLC_SRC}/build-${ARCH}"
mkdir -p "${BUILD_DIR}"

CONFIG_FLAGS=(
  "--prefix=${INSTALL_PREFIX}"
  "--with-contrib=${VLC_SRC}/contrib/${TRIPLET}"
  --disable-vlc          # libvlc only, no player binary
  --disable-qt
  --disable-skins2
  --disable-nls
  --disable-lua
  --disable-a52
  --disable-sid
  --enable-avcodec       # FFmpeg-based decoders for the test media
  --enable-swscale
  --enable-dvbpsi
  --disable-vdpau
  --disable-mad
  --disable-sdl-image   # not needed for tests; avoids broken cross link paths
  --disable-xcb          # headless: video verified via vmem callbacks, no X11
  --disable-alsa         # headless: audio verified via amem callbacks
  --disable-pulse
  --disable-vnc          # not needed; avoids linking gnutls/nettle
  --disable-gnutls       # local files only; no TLS module
  --disable-srt          # not needed; avoids linking gnutls/nettle
  --disable-chromaprint  # not needed; avoids non-PIC libavcodec FFT link error
)
[ "${CROSS}" = "1" ] && CONFIG_FLAGS+=("--host=${TRIPLET}" "--build=x86_64-linux-gnu")

(
  cd "${BUILD_DIR}"
  ../configure "${CONFIG_FLAGS[@]}"
  make -j"$(jobs)"
  make install
)

normalize_output "${RID}" "${INSTALL_PREFIX}"
cp -a "${VLC_SRC}/COPYING"     "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
cp -a "${VLC_SRC}/COPYING.LIB" "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
