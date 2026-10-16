#!/usr/bin/env bash
#
# Build libvlc for Windows (x64 / arm64) with llvm-mingw (UCRT) against the FULL
# VLC contrib set, built in-CI and cached in-repo (see build/lib.sh).
#
# This uses an explicit contrib + configure flow (not extras/package/win32/
# build.sh) so the contrib and libvlc phases can run as separate actions.
#
# Two phases, selected by BUILD_PHASE:
#   BUILD_PHASE=contrib  build-windows.sh <x86_64|aarch64>  -> cache contribs
#   build-windows.sh <x86_64|aarch64>          (default libvlc) -> build libvlc
#
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ARCH="${1:?arch required: x86_64 | aarch64}"
PHASE="${BUILD_PHASE:-libvlc}"
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
# VLC plugins are linked as modules; libtool drops the default libraries, so the
# compiler-rt builtins that provide __chkstk (the stack-probe helper the
# compiler emits for large frames) go missing and the plugin link fails with
# "undefined symbol: __chkstk". Append the builtins archive to every link step.
RT_BUILTINS="$("${LLVM_MINGW_DIR}/bin/${ARCH}-w64-mingw32-clang" --print-libgcc-file-name 2>/dev/null || true)"
[ -f "${RT_BUILTINS}" ] && EXTRA_LINK_FLAGS="${EXTRA_LINK_FLAGS} ${RT_BUILTINS}"
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
  # mpg123's autoreconf fails under the llvm-mingw cross environment (its
  # configure.ac uses the libtool macro LT_SYS_MODULE_EXT, which does not expand
  # here). MP3 decode is covered by mad + ffmpeg, so drop mpg123 on Windows.
  WIN_PRUNE="--disable-mpg123 ${CONTRIB_EXTRA_PRUNE:-}"
  if [ -n "${CONTRIB_GROUP:-}" ]; then
    build_contrib_group "${VLC_SRC}" "contrib-win-${ARCH}" "${TRIPLET}" "${RID}" "${CONTRIB_GROUP}" \
      -- ${WIN_PRUNE}
  else
    build_full_contrib "${VLC_SRC}" "contrib-win-${ARCH}" "${TRIPLET}" \
      -- ${WIN_PRUNE}
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

BUILD_DIR="${VLC_SRC}/build-win-${ARCH}"
rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}"

CONFIG_FLAGS=(
  "--prefix=${INSTALL_PREFIX}"
  "--host=${TRIPLET}"
  "--with-contrib=${CONTRIB_PREFIX}"
  --disable-vlc --disable-lua --disable-nls --disable-update-check
  --disable-goom --disable-libcddb
  --disable-fontconfig   # Windows uses the DirectWrite font provider, not fontconfig
  # Feature parity (contrib-provided; --enable so a missing contrib fails loud):
  --enable-taglib                                    # metadata read/write
  --enable-dvdread --enable-dvdnav --enable-bluray   # local disc stack
  # Native transport/network stack (contrib net group). gnutls provides TLS for
  # http/https + native HLS/DASH; live555 provides RTSP input + RTSP VOD serving;
  # smb2/srt/rist/microdns/nfs auto-enable from pkg-config. The Win32 BDA dtv
  # tuner and the Win32 smb (WNet, VLC3) / smb2 (VLC4) access, plus the DirectWrite
  # font provider, autodetect from the Windows SDK headers (llvm-mingw UCRT).
  --enable-gnutls --enable-live555 --enable-dvbpsi
  # zvbi (teletext), aribcam (ARIB B25) and sid (SID music) auto-detect from the
  # contrib set; left as auto so a target that cannot build one does not hard-fail.
  --enable-avcodec --enable-swscale
  --enable-shared --disable-static
)

# Shared product module policy (configure-level removals, e.g. --disable-ncurses).
vlc_product_config_flags
CONFIG_FLAGS+=("${VLC_PRODUCT_CONFIG_FLAGS[@]}")

(
  cd "${BUILD_DIR}"
  PKG_CONFIG_LIBDIR="${CONTRIB_PREFIX}/lib/pkgconfig" ../configure "${CONFIG_FLAGS[@]}"
  make -j"$(jobs)"
  make install
)

normalize_output "${RID}" "${INSTALL_PREFIX}"
cp -a "${VLC_SRC}/COPYING"     "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
cp -a "${VLC_SRC}/COPYING.LIB" "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
