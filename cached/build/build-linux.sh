#!/usr/bin/env bash
#
# Build libvlc for Linux (x64 / arm / arm64) against the FULL VLC contrib set,
# built in-CI and cached in-repo (see cached/build/lib.sh).
#
# Unlike prebuilt/build/build-linux.sh (which links the distro -dev packages),
# this pipeline builds the entire dependency closure from source so the runtime
# is self-contained and reproducible. arm/arm64 build natively inside a
# QEMU-emulated ubuntu:22.04 container (the workflow sets that up); this script
# itself is architecture-agnostic.
#
# Two phases, selected by CACHED_PHASE:
#   CACHED_PHASE=contrib  build-linux.sh <x86_64|armv7|aarch64>   -> cache contribs
#   build-linux.sh <x86_64|armv7|aarch64>         (default libvlc) -> build libvlc
#
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ARCH="${1:?arch required: x86_64 | armv7 | aarch64}"
PHASE="${CACHED_PHASE:-libvlc}"
case "${ARCH}" in
  x86_64)  RID="linux-x64";   TRIPLET="x86_64-linux-gnu" ;;
  armv7)   RID="linux-arm";   TRIPLET="arm-linux-gnueabihf" ;;
  aarch64) RID="linux-arm64"; TRIPLET="aarch64-linux-gnu" ;;
  *) die "Unsupported Linux arch: ${ARCH}" ;;
esac

VLC_SRC="${WORK_DIR}/vlc-linux-${ARCH}"
INSTALL_PREFIX="${WORK_DIR}/install-linux-${ARCH}"
mkdir -p "${WORK_DIR}"

# sidplay (SID codec) legacy C++ trips narrowing diagnostics on modern GCC.
export CXXFLAGS="${CXXFLAGS:-} -Wno-narrowing"

clone_vlc "${VLC_SRC}"
apply_patches "${VLC_SRC}"

# ---- Phase 1: build + cache the full contrib set ----------------------------
if [ "${PHASE}" = "contrib" ]; then
  # Cross-compile the contrib set when the runner architecture differs from the
  # target (building arm/arm64 contribs on an x86_64 runner). A full contrib
  # closure under QEMU emulation is far too slow; the contrib build needs no
  # target execution, so cross-compiling is both fast and reliable. (The libvlc
  # phase still runs on the target under QEMU for its ldd-based bundling.)
  if [ "${ARCH}" != "x86_64" ] && [ "$(uname -m)" = "x86_64" ]; then
    log "Cross-compiling ${RID} contribs with the ${TRIPLET} toolchain"
    export CC="${TRIPLET}-gcc" CXX="${TRIPLET}-g++" \
           AR="${TRIPLET}-ar" RANLIB="${TRIPLET}-ranlib" \
           STRIP="${TRIPLET}-strip" NM="${TRIPLET}-nm" LD="${TRIPLET}-ld"
    command -v "${CC}" >/dev/null 2>&1 || die "cross toolchain ${CC} not installed"
  fi
  build_full_contrib "${VLC_SRC}" "contrib-linux-${ARCH}" "${TRIPLET}" \
    ${CACHED_CONTRIB_PRUNE:+-- ${CACHED_CONTRIB_PRUNE}}
  pack_contrib "${VLC_SRC}" "${TRIPLET}" "${RID}"
  exit 0
fi

# ---- Phase 2: restore contribs + build libvlc -------------------------------
restore_contrib "${VLC_SRC}" "${TRIPLET}" "${RID}"
CONTRIB_PREFIX="${VLC_SRC}/contrib/${TRIPLET}"

( cd "${VLC_SRC}" && ./bootstrap )

BUILD_DIR="${VLC_SRC}/build-${ARCH}"
rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}"

CONFIG_FLAGS=(
  "--prefix=${INSTALL_PREFIX}"
  "--with-contrib=${CONTRIB_PREFIX}"
  --disable-vlc          # libvlc only, no player binary
  --disable-qt --disable-skins2 --disable-nls --disable-lua --disable-a52
  --enable-avcodec       # FFmpeg-based decoders (from contrib)
  --enable-swscale
  --disable-vdpau --disable-sdl-image
  --disable-xcb          # headless: video verified via vmem callbacks
  --disable-alsa         # headless: audio verified via amem callbacks
  --disable-pulse
  --disable-bluray --disable-vnc --disable-gnutls --disable-srt --disable-chromaprint
  --disable-taglib       # metadata reader: not a required test module
  --enable-shared --disable-static
)

(
  cd "${BUILD_DIR}"
  PKG_CONFIG_PATH="${CONTRIB_PREFIX}/lib/pkgconfig" ../configure "${CONFIG_FLAGS[@]}"
  make -j"$(jobs)"
  make install
)

normalize_output "${RID}" "${INSTALL_PREFIX}"

# Make the artifact relocatable: bundle the non-system shared-object closure
# next to libvlc and rewrite rpaths to $ORIGIN (needs to run on the target arch,
# which is why arm/arm64 build under QEMU).
bundle_linux_deps "${ARTIFACTS_DIR}/${RID}"

cp -a "${VLC_SRC}/COPYING"     "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
cp -a "${VLC_SRC}/COPYING.LIB" "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
