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
  BOOT_ARGS=(--disable-all --enable-ffmpeg --enable-opus --enable-ogg --enable-ass --enable-matroska --disable-net --disable-sout --disable-disc)
  if [ "${CROSS}" = "1" ]; then
    # Explicit --build is required so autoconf treats this as a cross build and
    # never tries to execute target binaries on the x86_64 runner.
    BOOT_ARGS+=(--host="${TRIPLET}" --build="x86_64-linux-gnu")
  fi
  ../bootstrap "${BOOT_ARGS[@]}"
  prefetch_contrib_tarballs "${VLC_SRC}/contrib/tarballs"
  make -j"$(jobs)" fetch
  if ! make -j"$(jobs)"; then
    warn "Parallel contrib build failed; retrying serially for a clean error"
    if ! make -j1; then
      warn "Contrib build failed. Dumping ffmpeg config.log tails for diagnosis:"
      find "${CONTRIB_BUILD}" -path '*ffmpeg*/config.log' \
        -exec sh -c 'echo "----- $1 -----"; tail -n 80 "$1"' _ {} \; 2>/dev/null || true
      exit 1
    fi
  fi
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
rm -rf "${BUILD_DIR}"
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
  --disable-fast-install # cross libtool otherwise defers the real libvlccore.so
)
[ "${CROSS}" = "1" ] && CONFIG_FLAGS+=("--host=${TRIPLET}" "--build=x86_64-linux-gnu")

(
  cd "${BUILD_DIR}"
  ../configure "${CONFIG_FLAGS[@]}"
  if [ "${CROSS}" = "1" ]; then
    log "Cross build: relaxing libvlccore libtool flags in generated src/Makefile"
    cat >> src/Makefile <<'EOF'

override libvlccore_la_LDFLAGS := $(filter-out -no-undefined -export-symbols %libvlccore.sym,$(libvlccore_la_LDFLAGS))
EOF
  fi
  # Build the support lib and libvlccore before the plugins. Under a single
  # "make -j" the recursive src/ and modules/ sub-makes overlap on the cross
  # toolchains, so a plugin can try to link ../src/.libs/libvlccore.so before
  # libtool has finalized it. Building compat + src first is deterministic.
  make -j"$(jobs)" -C compat
  if [ "${CROSS}" = "1" ]; then
    # The aarch64/armv7 cross libtool has been observed to emit only
    # libvlccore.la + dangling dev symlinks, never the real libvlccore.so.N
    # (make -C src install then fails: cannot stat .libs/libvlccore.so.9.0.1).
    # Trace the actual libvlccore link so the real ld/libtool command is
    # captured, then list what landed in src/.libs.
    log "Cross build: tracing the libvlccore link (V=1) for diagnosis"
    make -C src V=1 libvlccore.la 2>&1 | tee "${WORK_DIR}/libvlccore-${ARCH}.log" || true
    log "libvlccore artifacts produced under src/.libs:"
    ls -la src/.libs/ 2>/dev/null | grep -i vlccore || log "(no libvlccore.* in src/.libs)"
    log "last 60 lines of the libvlccore link trace:"
    tail -n 60 "${WORK_DIR}/libvlccore-${ARCH}.log" || true
  fi
  make -j"$(jobs)" -C src
  if [ "${CROSS}" = "1" ]; then
    shopt -s nullglob
    core_real=(src/.libs/libvlccore.so.*.*.*)
    shopt -u nullglob
    if [ ${#core_real[@]} -eq 0 ]; then
      warn "Cross build did not emit a versioned libvlccore shared object after make -C src; forcing a serial relink"
      if ./libtool --config 2>/dev/null | grep -q '^build_libtool_libs=no$'; then
        warn "libtool reports build_libtool_libs=no for the cross build; forcing shared-library mode before relink"
        perl -0pi -e 's/^build_libtool_libs=no$/build_libtool_libs=yes/m; s/^build_old_libs=yes$/build_old_libs=no/m' libtool
      fi
      log "evaluated libtool shared-library mode before relink:"
      ./libtool --config 2>/dev/null | grep -E '^(build_libtool_libs|build_old_libs)=' || true
      make -C src V=1 -B libvlccore.la 2>&1 | tee "${WORK_DIR}/libvlccore-relink-${ARCH}.log" || true
      shopt -s nullglob
      core_real=(src/.libs/libvlccore.so.*.*.*)
      shopt -u nullglob
    fi
    if [ ${#core_real[@]} -gt 0 ]; then
      core_base="$(basename "${core_real[0]}")"
      core_soname="${core_base%.*}"
      ln -sf "${core_base}" "src/.libs/${core_soname}"
      ln -sf "${core_soname}" src/.libs/libvlccore.so
      log "Synthesized cross-build libvlccore symlinks: libvlccore.so -> ${core_soname} -> ${core_base}"
    else
      warn "Cross build still has no versioned libvlccore shared object under src/.libs after forced relink"
      log "evaluated libtool shared-library mode after forced relink:"
      ./libtool --config 2>/dev/null | grep -E '^(build_libtool_libs|build_old_libs)=' || true
      log "src/.libs/libvlccore.lai contents:"
      sed -n '1,160p' src/.libs/libvlccore.lai 2>/dev/null || true
      log "last 80 lines of the forced relink trace:"
      tail -n 80 "${WORK_DIR}/libvlccore-relink-${ARCH}.log" 2>/dev/null || true
      ls -la src/.libs/ 2>/dev/null | grep -i vlccore || true
      exit 1
    fi
  fi
  make -j"$(jobs)"
  make install
)

normalize_output "${RID}" "${INSTALL_PREFIX}"
cp -a "${VLC_SRC}/COPYING"     "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
cp -a "${VLC_SRC}/COPYING.LIB" "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
