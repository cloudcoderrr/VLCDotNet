#!/usr/bin/env bash
#
# Build libvlc for Windows (x64 / arm64) with llvm-mingw (UCRT) against the FULL
# VLC contrib set, built in-CI and cached in-repo (see cached/build/lib.sh).
#
# This uses an explicit contrib + configure flow (not extras/package/win32/
# build.sh) so the contrib and libvlc phases can run as separate actions.
#
# Two phases, selected by CACHED_PHASE:
#   CACHED_PHASE=contrib  build-windows.sh <x86_64|aarch64>  -> cache contribs
#   build-windows.sh <x86_64|aarch64>          (default libvlc) -> build libvlc
#
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ARCH="${1:?arch required: x86_64 | aarch64}"
PHASE="${CACHED_PHASE:-libvlc}"
case "${ARCH}" in
  x86_64)  RID="win-x64";   TRIPLET="x86_64-w64-mingw32" ;;
  aarch64) RID="win-arm64"; TRIPLET="aarch64-w64-mingw32" ;;
  *) die "Unsupported Windows arch: ${ARCH} (expected x86_64 or aarch64)" ;;
esac

VLC_SRC="${WORK_DIR}/vlc-win-${ARCH}"
INSTALL_PREFIX="${WORK_DIR}/install-win-${ARCH}"
mkdir -p "${WORK_DIR}"

clone_vlc "${VLC_SRC}"
apply_patches "${VLC_SRC}"

# llvm-mingw (UCRT, current Windows SDK headers). apt mingw headers are too old
# for VLC's d3d11 output, and only llvm-mingw provides an aarch64 target.
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

# Wrap the gcc/g++ names VLC's contrib + configure invoke so they resolve to
# llvm-mingw clang, target the Windows 10 API level (so d3d11/DXGI types are
# available), and downgrade VLC 3's clang-default-error diagnostics. Only the
# cross compilers are affected; native tools stay untouched.
WRAP_DIR="${WORK_DIR}/xwrap-${ARCH}"
mkdir -p "${WRAP_DIR}"
LENIENT="-Wno-error=incompatible-function-pointer-types -Wno-error=incompatible-pointer-types -Wno-error=implicit-function-declaration -Wno-error=int-conversion"
WINVER_FLAGS="-D_WIN32_WINNT=0x0A000000 -DWINVER=0x0A00"
EXTRA_LINK_FLAGS=""
# x86_64 UCRT contribs (freetype, SDL_image) emit _setjmp references; resolve
# them via mingw-w64's support library at cross-link time.
[ "${ARCH}" = "x86_64" ] && EXTRA_LINK_FLAGS="-lmingwex"
for pair in "gcc:clang" "g++:clang++" "clang:clang" "clang++:clang++" ; do
  name="${pair%%:*}"; realt="${pair##*:}"
  cat > "${WRAP_DIR}/${ARCH}-w64-mingw32-${name}" <<EOF
#!/bin/sh
for arg in "\$@"; do
  case "\$arg" in
    -c|-E|-S)
      exec "${LLVM_MINGW_DIR}/bin/${ARCH}-w64-mingw32-${realt}" ${LENIENT} ${WINVER_FLAGS} "\$@"
      ;;
  esac
done
exec "${LLVM_MINGW_DIR}/bin/${ARCH}-w64-mingw32-${realt}" ${LENIENT} ${WINVER_FLAGS} "\$@" ${EXTRA_LINK_FLAGS}
EOF
  chmod +x "${WRAP_DIR}/${ARCH}-w64-mingw32-${name}"
done
export PATH="${WRAP_DIR}:${PATH}"

# ---- Phase 1: build + cache the full contrib set ----------------------------
if [ "${PHASE}" = "contrib" ]; then
  build_full_contrib "${VLC_SRC}" "contrib-win-${ARCH}" "${TRIPLET}" \
    ${CACHED_CONTRIB_PRUNE:+-- ${CACHED_CONTRIB_PRUNE}}
  pack_contrib "${VLC_SRC}" "${TRIPLET}" "${RID}"
  exit 0
fi

# ---- Phase 2: restore contribs + build libvlc -------------------------------
restore_contrib "${VLC_SRC}" "${TRIPLET}" "${RID}"
CONTRIB_PREFIX="${VLC_SRC}/contrib/${TRIPLET}"

# Drop contrib TLS/streaming libs so libvlc does not pull network modules we do
# not ship in the current test surface.
rm -f "${CONTRIB_PREFIX}"/lib/libgnutls* "${CONTRIB_PREFIX}"/lib/pkgconfig/gnutls.pc \
      "${CONTRIB_PREFIX}"/lib/libsrt*    "${CONTRIB_PREFIX}"/lib/pkgconfig/srt.pc 2>/dev/null || true

( cd "${VLC_SRC}" && ./bootstrap )

BUILD_DIR="${VLC_SRC}/build-win-${ARCH}"
rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}"

CONFIG_FLAGS=(
  "--prefix=${INSTALL_PREFIX}"
  "--host=${TRIPLET}"
  "--with-contrib=${CONTRIB_PREFIX}"
  --disable-vlc --disable-lua --disable-nls --disable-update-check
  --disable-live555 --disable-realrtsp --disable-goom --disable-libcddb
  --disable-shout --disable-zvbi --disable-dvdread --disable-dvdnav
  --disable-bluray --disable-gnutls --disable-srt --disable-aribcam
  --disable-fontconfig
  --disable-taglib       # metadata reader: not a required test module
  --disable-sid          # SID is not a required module on Windows and the
                         # llvm-mingw aarch64 link fails (undefined __chkstk)
  --enable-avcodec --enable-swscale
  --enable-shared --disable-static
)

(
  cd "${BUILD_DIR}"
  PKG_CONFIG_LIBDIR="${CONTRIB_PREFIX}/lib/pkgconfig" ../configure "${CONFIG_FLAGS[@]}"
  make -j"$(jobs)"
  make install
)

normalize_output "${RID}" "${INSTALL_PREFIX}"
cp -a "${VLC_SRC}/COPYING"     "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
cp -a "${VLC_SRC}/COPYING.LIB" "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
