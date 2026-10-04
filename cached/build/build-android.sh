#!/usr/bin/env bash
#
# Build libvlc for Android (arm / arm64 / x86_64) against the FULL VLC contrib
# set, built in-CI and cached in-repo (see cached/build/lib.sh).
#
# Two phases, selected by CACHED_PHASE:
#   CACHED_PHASE=contrib  build-android.sh <arm|arm64|x86_64>
#       Build the full contrib set with the Android NDK and cache it under
#       cached/contribs/vlc-<series>.0/<rid>/ (no libvlc build).
#   build-android.sh <arm|arm64|x86_64>            (default: CACHED_PHASE=libvlc)
#       Restore the cached contrib set and build libvlc -> artifacts/<rid>.
#
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ARCH="${1:?arch required: arm|arm64|x86_64}"
API="${ANDROID_API:-21}"
PHASE="${CACHED_PHASE:-libvlc}"

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
  build_full_contrib "${VLC_SRC}" "contrib-android-${ARCH}" "${TRIPLET}" \
    ${CACHED_CONTRIB_PRUNE:+-- ${CACHED_CONTRIB_PRUNE}}
  pack_contrib "${VLC_SRC}" "${TRIPLET}" "${RID}"
  exit 0
fi

# ---- Phase 2: restore contribs + build libvlc -------------------------------
restore_contrib "${VLC_SRC}" "${TRIPLET}" "${RID}"
CONTRIB_PREFIX="${VLC_SRC}/contrib/${TRIPLET}"

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
  --disable-taglib
  --disable-sdl-image
  --disable-bluray
  --disable-xcb --disable-alsa --disable-pulse --disable-vdpau
  --disable-v4l2 --disable-vnc --disable-gnutls --disable-srt --disable-ncurses
  --disable-libxml2
  --enable-avcodec --enable-swscale
  --enable-shared --disable-static
)

# API 21's sys/shm.h is a stub; force VLC's non-shm fallback path in block.c.
CONFIG_ENV=("${CONTRIB_ENV[@]}" ac_cv_header_sys_shm_h=no)

# NDK r29 no longer implicitly links libm into module plugins and folds pthread
# into libc; provide -lm plus an empty libpthread.a for modules linking it.
PTHREAD_STUB_DIR="${WORK_DIR}/pthread-stub-${ARCH}"
mkdir -p "${PTHREAD_STUB_DIR}"
"${AR}" rc "${PTHREAD_STUB_DIR}/libpthread.a"
export LDFLAGS="${LDFLAGS:-} -L${PTHREAD_STUB_DIR} -lm"

if [ "${ARCH}" = "arm" ] || [ "${ARCH}" = "arm64" ]; then
  # armv7 needs compiler-rt for 64-bit int<->float; aarch64 needs the
  # outline-atomics helpers (__aarch64_ldadd8_*) VLC 4.0's vpx_alpha plugin uses.
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

# VLC 4.0's C++ plugins (mkv/libmatroska, spatializer, adaptive, vpx_alpha,
# blend, ...) dynamically link the NDK's shared C++ runtime, which is not part
# of the VLC install prefix. Ship it alongside libvlc.so so the APK layout puts
# it in lib/<abi>/ and those plugins can dlopen at runtime. VLC 3.0's plugin set
# does not pull in libc++_shared, so this is limited to 4.x.
if [ "${VLC_SERIES}" != "3" ]; then
  libcxx="${NDK_TC}/sysroot/usr/lib/${TRIPLET}/libc++_shared.so"
  if [ -f "${libcxx}" ]; then
    cp -a "${libcxx}" "${ARTIFACTS_DIR}/${RID}/"
    log "Bundled libc++_shared.so for ${RID}"
  else
    warn "libc++_shared.so not found at ${libcxx}"
  fi
fi

cp -a "${VLC_SRC}/COPYING"     "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
cp -a "${VLC_SRC}/COPYING.LIB" "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
