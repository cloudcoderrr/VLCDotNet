#!/usr/bin/env bash
#
# Build libvlc for Android (arm / arm64 / x86_64) against the FULL VLC contrib
# set, built in-CI and cached in-repo (see build/lib.sh).
#
# Two phases, selected by BUILD_PHASE:
#   BUILD_PHASE=contrib  build-android.sh <arm|arm64|x86_64>
#       Build the full contrib set with the Android NDK and cache it under
#       contribs/vlc-<series>.0/<rid>/ (no libvlc build).
#   build-android.sh <arm|arm64|x86_64>            (default: BUILD_PHASE=libvlc)
#       Restore the cached contrib set and build libvlc -> artifacts/<rid>.
#
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ARCH="${1:?arch required: arm|arm64|x86_64}"
API="${ANDROID_API:-21}"
PHASE="${BUILD_PHASE:-libvlc}"

case "${ARCH}" in
  arm)    TARGET="armv7a-linux-androideabi"; TRIPLET="arm-linux-androideabi";  RID="android-arm";   ABI="armeabi-v7a" ;;
  arm64)  TARGET="aarch64-linux-android";    TRIPLET="aarch64-linux-android";  RID="android-arm64"; ABI="arm64-v8a" ;;
  x86_64) TARGET="x86_64-linux-android";     TRIPLET="x86_64-linux-android";   RID="android-x64";   ABI="x86_64" ;;
  *) die "Unsupported Android arch: ${ARCH}" ;;
esac

: "${ANDROID_NDK_HOME:?ANDROID_NDK_HOME must point at the Android NDK}"
NDK_TC="${ANDROID_NDK_HOME}/toolchains/llvm/prebuilt/linux-x86_64"
[ -d "${NDK_TC}" ] || die "NDK toolchain not found at ${NDK_TC}"

export CC="${NDK_TC}/bin/${TARGET}${API}-clang"
export CXX="${NDK_TC}/bin/${TARGET}${API}-clang++"
export AR="${NDK_TC}/bin/llvm-ar"
export RANLIB="${NDK_TC}/bin/llvm-ranlib"
export STRIP="${NDK_TC}/bin/llvm-strip"
export NM="${NDK_TC}/bin/llvm-nm"
export LD="${NDK_TC}/bin/ld"
export PATH="${NDK_TC}/bin:${PATH}"
export ANDROID_NDK="${ANDROID_NDK_HOME}"
export ANDROID_ABI="${ABI}"

# NDK r23+ ships only the llvm-* binutils; some contrib configure scripts (x264)
# invoke the unprefixed names (strings, nm, ...) directly from the toolchain bin
# dir. Provide the missing symlinks so those probes resolve to the llvm tools.
for _t in strings nm ar ranlib strip objcopy objdump readelf size addr2line; do
  [ -e "${NDK_TC}/bin/${_t}" ] || ln -sf "llvm-${_t}" "${NDK_TC}/bin/${_t}" 2>/dev/null || true
done
unset _t
# Downgrade legacy C/C++ diagnostics the NDK clang treats as errors in VLC 3.
export CFLAGS="${CFLAGS:-} -Wno-error=implicit-function-declaration -Wno-error=incompatible-pointer-types -Wno-error=int-conversion"
export CXXFLAGS="${CXXFLAGS:-} -Wno-error=incompatible-pointer-types -Wno-c++11-narrowing"

if [ "${ARCH}" = "arm" ]; then
  export CFLAGS="${CFLAGS} -D_LARGEFILE_SOURCE -D_FILE_OFFSET_BITS=64"
  export CXXFLAGS="${CXXFLAGS} -D_LARGEFILE_SOURCE -D_FILE_OFFSET_BITS=64 -D_LIBCPP_HAS_NO_OFF_T_FUNCTIONS"
  export AS="${CC}"
fi

VLC_SRC="${WORK_DIR}/vlc-android-${ARCH}"
INSTALL_PREFIX="${WORK_DIR}/install-android-${ARCH}"
mkdir -p "${WORK_DIR}"

clone_vlc "${VLC_SRC}"
apply_patches "${VLC_SRC}"

# 32-bit ARM needs the armv7 ffmpeg FPU flags and the bionic sidplay2 fstream
# compat fix for the (contrib) codec sources; scoped so other arches are clean.
if [ "${ARCH}" = "arm" ]; then
  shopt -s nullglob
  for p in "${PATCH_DIR}"/android-arm/*.patch; do
    log "Applying android-arm patch $(basename "$p")"
    git -C "${VLC_SRC}" apply --recount --whitespace=nowarn "$p" 2>/dev/null \
      || ( cd "${VLC_SRC}" && patch -p1 --forward --fuzz=3 --no-backup-if-mismatch < "$p" )
  done
  shopt -u nullglob
fi

CONTRIB_ENV=(HAVE_ANDROID=1 ANDROID_API="${API}" ANDROID_ABI="${ABI}" ANDROID_NDK="${ANDROID_NDK_HOME}")

# ---- Phase 1: build + cache the full contrib set ----------------------------
if [ "${PHASE}" = "contrib" ]; then
  # Android ships no optical-disc hardware, so drop the disc stack (dvd*/bluray)
  # from the parity closure (it is desktop-only and bluray needs fontconfig,
  # which Android does not use). mpg123's autoreconf also fails under the NDK
  # cross env (LT_SYS_MODULE_EXT); MP3 decode is covered by mad + ffmpeg.
  # Network dependencies remain enabled by the baseline contrib configuration.
  ANDROID_PRUNE="--disable-disc --disable-mpg123 ${CONTRIB_EXTRA_PRUNE:-}"
  # VLC 4.0's sidplay2 does not compile against the NDK libc++ (<fstream>
  # references ::fseeko/::ftello) on Android; SID music is deferred on VLC 4
  # Android (it builds fine for VLC 3). Scope the prune to that combination.
  [ "${VLC_SERIES}" = "4" ] && ANDROID_PRUNE="--disable-sidplay2 ${ANDROID_PRUNE}"
  if [ -n "${CONTRIB_GROUP:-}" ]; then
    build_contrib_group "${VLC_SRC}" "contrib-android-${ARCH}" "${TRIPLET}" "${RID}" "${CONTRIB_GROUP}" \
      -- ${ANDROID_PRUNE}
  else
    build_full_contrib "${VLC_SRC}" "contrib-android-${ARCH}" "${TRIPLET}" \
      -- ${ANDROID_PRUNE}
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

# libvncclient references wait3(), which Android's bionic libc does not provide,
# so the vnc plugin fails to link (undefined symbol: wait3). VNC is not a
# restoration feature; drop its pkg-config entry so VLC skips the vnc plugin on
# Android (it still builds on the glibc/mingw desktop targets).
rm -f "${CONTRIB_PREFIX}"/lib/pkgconfig/libvncclient.pc 2>/dev/null || true

( cd "${VLC_SRC}" && ./bootstrap )

BUILD_DIR="${VLC_SRC}/build-android-${ARCH}"
rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}"

CONFIG_FLAGS=(
  "--prefix=${INSTALL_PREFIX}"
  "--host=${TRIPLET}"
  "--with-contrib=${CONTRIB_PREFIX}"
  --disable-vlc --disable-qt --disable-skins2 --disable-nls
  --disable-lua --disable-a52
  --enable-taglib        # metadata read/write (feature parity, contrib-provided)
  --disable-sdl-image
  --disable-bluray       # Android ships no optical-disc stack
  --disable-xcb --disable-alsa --disable-pulse --disable-vdpau
  --disable-v4l2 --disable-ncurses
  # Native transport/network stack (contrib net group). gnutls provides TLS for
  # http/https + native HLS/DASH; live555 provides RTSP input + VOD; smb2/srt/
  # rist/microdns/nfs auto-enable from pkg-config. VA-API/VDPAU have no Android
  # backend (hardware decode is MediaCodec, enabled separately).
  --enable-gnutls --enable-live555 --enable-dvbpsi
  --enable-avcodec --enable-swscale
  --enable-shared --disable-static
)

# API 21's sys/shm.h is a stub; force VLC's non-shm fallback path in block.c.
CONFIG_ENV=("${CONTRIB_ENV[@]}" ac_cv_header_sys_shm_h=no)

# VLC 4.0 pulls in libass/fontconfig text plugins whose (static) link references
# the contrib gettext wrappers (libintl_dgettext/newlocale/freelocale) and
# libiconv. Append them via autoconf LIBS so they land after the referencing
# archives on the plugin link line (order-sensitive with lld). VLC 3 unaffected.
# GNUTLS_STATIC_CLOSURE (the gnutls nettle closure, set by export_gnutls_static_libs)
# is folded in here because this explicit LIBS overrides the ambient export.
if [ "${VLC_SERIES}" != "3" ]; then
  CONFIG_ENV+=("LIBS=-lintl -liconv ${GNUTLS_STATIC_CLOSURE:-}")
fi

# NDK r29 no longer implicitly links libm into module plugins and folds pthread
# into libc; provide -lm plus an empty libpthread.a for modules linking it.
PTHREAD_STUB_DIR="${WORK_DIR}/pthread-stub-${ARCH}"
mkdir -p "${PTHREAD_STUB_DIR}"
"${AR}" rc "${PTHREAD_STUB_DIR}/libpthread.a"
export LDFLAGS="${LDFLAGS:-} -L${PTHREAD_STUB_DIR} -lm"

# VLC 4.0: the libass/fontconfig text plugins need the contrib gettext/iconv
# archives (LIBS=-lintl -liconv, set above). Put the contrib lib dir on the
# search path so those resolve everywhere, including configure's own link checks.
if [ "${VLC_SERIES}" != "3" ]; then
  export LDFLAGS="${LDFLAGS} -L${CONTRIB_PREFIX}/lib"
fi

if [ "${ARCH}" = "arm" ] || [ "${ARCH}" = "arm64" ] || [ "${ARCH}" = "x86_64" ]; then
  # All Android ABIs need the compiler-rt builtins for some plugins: armv7 for
  # 64-bit int<->float, aarch64 for the outline-atomics helpers
  # (__aarch64_ldadd8_*), and x86_64 for the 128-bit long-double soft-float
  # routines (__floatunditf/__divtf3/...) that taglib's C++ pulls in.
  RT_BUILTINS="$(${CC} --print-libgcc-file-name 2>/dev/null || true)"
  if [ -f "${RT_BUILTINS}" ]; then
    export LDFLAGS="${LDFLAGS} -L$(dirname "${RT_BUILTINS}") -l:$(basename "${RT_BUILTINS}")"
  fi
fi

(
  cd "${BUILD_DIR}"
  env "${CONFIG_ENV[@]}" ../configure "${CONFIG_FLAGS[@]}"
  env "${CONFIG_ENV[@]}" make -j"$(jobs)"
  make install
)

normalize_output "${RID}" "${INSTALL_PREFIX}"

# VLC's C++ plugins (mkv/libmatroska, taglib, sid, gme, modplug, spatializer,
# adaptive, blend, ...) dynamically link the NDK's shared C++ runtime, which is
# not part of the VLC install prefix. Ship it alongside libvlc.so so the APK
# layout puts it in lib/<abi>/ and those plugins can dlopen at runtime. The
# parity closure pulls C++ plugins into both series, so bundle it unconditionally.
libcxx="${NDK_TC}/sysroot/usr/lib/${TRIPLET}/libc++_shared.so"
if [ -f "${libcxx}" ]; then
  cp -a "${libcxx}" "${ARTIFACTS_DIR}/${RID}/"
  log "Bundled libc++_shared.so for ${RID}"
else
  warn "libc++_shared.so not found at ${libcxx}"
fi

cp -a "${VLC_SRC}/COPYING"     "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
cp -a "${VLC_SRC}/COPYING.LIB" "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
