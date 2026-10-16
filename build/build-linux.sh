#!/usr/bin/env bash
#
# Build libvlc for Linux (x64 / arm / arm64) against the FULL VLC contrib set,
# built in-CI and cached in-repo (see build/lib.sh).
#
# Full-source native builds compile the dependency closure so the runtime
# is self-contained and reproducible. arm/arm64 build natively inside a
# QEMU-emulated ubuntu:22.04 container (the workflow sets that up); this script
# itself is architecture-agnostic.
#
# Two phases, selected by BUILD_PHASE:
#   BUILD_PHASE=contrib  build-linux.sh <x86_64|armv7|aarch64>   -> cache contribs
#   build-linux.sh <x86_64|armv7|aarch64>         (default libvlc) -> build libvlc
#
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ARCH="${1:?arch required: x86_64 | armv7 | aarch64}"
PHASE="${BUILD_PHASE:-libvlc}"
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
    command -v "${TRIPLET}-gcc" >/dev/null 2>&1 || die "cross toolchain ${TRIPLET}-gcc not installed"
    # Deliberately do NOT export CC/CXX here: VLC's contrib bootstrap detects the
    # BUILD triplet via `cc -dumpmachine` and derives the cross compiler
    # ($(HOST)-gcc) from --host itself. Exporting CC=<triplet>-gcc would make it
    # detect BUILD==HOST, disable cross mode, and configure would then try to run
    # target binaries ("cannot run C compiled programs"). extras/tools must also
    # build as native host tools, which relies on the native cc.
  fi
  if [ -n "${CONTRIB_GROUP:-}" ]; then
    build_contrib_group "${VLC_SRC}" "contrib-linux-${ARCH}" "${TRIPLET}" "${RID}" "${CONTRIB_GROUP}" \
      ${CONTRIB_EXTRA_PRUNE:+-- ${CONTRIB_EXTRA_PRUNE}}
  else
    build_full_contrib "${VLC_SRC}" "contrib-linux-${ARCH}" "${TRIPLET}" \
      ${CONTRIB_EXTRA_PRUNE:+-- ${CONTRIB_EXTRA_PRUNE}}
    pack_contrib "${VLC_SRC}" "${TRIPLET}" "${RID}"
  fi
  exit 0
fi

# ---- Phase 2: restore contribs + build libvlc -------------------------------
restore_contrib "${VLC_SRC}" "${TRIPLET}" "${RID}"
CONTRIB_PREFIX="${VLC_SRC}/contrib/${TRIPLET}"

# Pin GNUTLS_LIBS to the contrib gnutls static closure (nettle/hogweed/gmp) so
# the TLS plugin links; VLC 3.0's contrib gnutls bundles a nettle backport that
# otherwise leaves undefined nettle_* references at the plugin link step.
export_gnutls_static_libs "${CONTRIB_PREFIX}"

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
  # Feature parity (contrib-provided; --enable so a missing contrib fails loud).
  --enable-taglib                                    # metadata read/write
  --enable-dvdread --enable-dvdnav --enable-bluray   # local disc stack
  # Native transport/network stack (contrib net group: gnutls/live555/librist/
  # smb2/microdns/nfs/srt/...). --enable the explicit ones so a missing contrib
  # fails loud; srt/rist/smb2/nfs/microdns/sat-ip auto-enable from pkg-config.
  --enable-gnutls        # TLS provider for http/https + native HLS/DASH
  --enable-live555       # RTSP input + RTSP VOD serving + realrtsp
  --enable-dvbpsi        # TS PSI tables + the Linux dtv/dvb tuner access
  --disable-sdl-image    # images covered by png/jpeg/avcodec
  # X11/XCB + OpenGL/EGL/GLES + Wayland display and ALSA/PulseAudio output
  # auto-detect from the -dev libs deps-linux.sh installs. Headless CI builds and
  # loads them; real desktop apps use them for video output / audio.
  --disable-chromaprint
  --enable-shared --disable-static
)

# VA-API/VDPAU hardware decode (Linux with a supported GPU/driver). The NATIVE
# x86_64 contrib ffmpeg autodetects the system libva/libdrm/libvdpau through the
# contrib PKG_CONFIG_LIBDIR (which includes /usr/lib/<triplet>/pkgconfig), so the
# vaapi/vdpau hwaccels link; --enable makes a regression fail loud. The arm/arm64
# contribs cross-build without the target libva, so hardware decode is left out
# there and avcodec falls back to software decode.
if [ "${ARCH}" = "x86_64" ]; then
  CONFIG_FLAGS+=(--enable-vaapi --enable-vdpau)
else
  CONFIG_FLAGS+=(--disable-vaapi --disable-vdpau)
fi

# Shared product module policy (configure-level removals, e.g. --disable-ncurses).
vlc_product_config_flags
CONFIG_FLAGS+=("${VLC_PRODUCT_CONFIG_FLAGS[@]}")

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
