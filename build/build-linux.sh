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
  # Build only the contrib closure needed for local-file playback of the test
  # media: ffmpeg decoders, Opus (ffmpeg's opus decoder is disabled), libass
  # subtitles and Matroska. --disable-all empties the default ~80-package set so
  # bootstrap resolves just these packages plus their dependencies, keeping every
  # download on a reachable upstream host instead of the unreliable VideoLAN
  # mirror.
  BOOT_ARGS=(--disable-all --enable-ffmpeg --enable-opus --enable-ass --enable-matroska --disable-net --disable-sout --disable-disc)
  if [ "${CROSS}" = "1" ]; then
    # Explicit --build is required so autoconf treats this as a cross build and
    # never tries to execute target binaries on the x86_64 runner.
    BOOT_ARGS+=(--host="${TRIPLET}" --build="x86_64-linux-gnu")
  fi
  ../bootstrap "${BOOT_ARGS[@]}"
  make -j"$(jobs)" fetch
  make -j"$(jobs)" || make -j1
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
  --disable-vdpau
  --disable-mad
  --disable-sdl-image   # not needed for tests; avoids broken cross link paths
  --disable-xcb          # headless: video verified via vmem callbacks, no X11
  --disable-alsa         # headless: audio verified via amem callbacks
  --disable-pulse
  --disable-bluray       # local-file playback only; avoid optical-disc deps
  --disable-vnc          # not needed; avoids linking gnutls/nettle
  --disable-gnutls       # local files only; no TLS module
  --disable-srt          # not needed; avoids linking gnutls/nettle
  --disable-chromaprint  # not needed; avoids non-PIC libavcodec FFT link error
  --enable-shared        # build libvlccore.so; plugins link it by name (cross)
  --disable-static
)
[ "${CROSS}" = "1" ] && CONFIG_FLAGS+=("--host=${TRIPLET}" "--build=x86_64-linux-gnu")

(
  cd "${BUILD_DIR}"
  ../configure "${CONFIG_FLAGS[@]}"
  # Build the support lib and libvlccore before the plugins. Under a single
  # "make -j" the recursive src/ and modules/ sub-makes overlap on the cross
  # toolchains, so a plugin can try to link ../src/.libs/libvlccore.so before
  # libtool has finalized it. Building compat + src first is deterministic.
  make -j"$(jobs)" -C compat
  make -j"$(jobs)" -C src
  make -j"$(jobs)"
  make install
)

normalize_output "${RID}" "${INSTALL_PREFIX}"
cp -a "${VLC_SRC}/COPYING"     "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
cp -a "${VLC_SRC}/COPYING.LIB" "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
