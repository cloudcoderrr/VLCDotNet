#!/usr/bin/env bash
#
# Build libvlc 3.0.x for Android (arm / arm64 / x86_64) using VideoLAN's
# PREBUILT contrib bundle (make prebuilt) with the Android NDK. Falls back to a
# full from-source contrib build if no prebuilt bundle is published for the host.
#
#   Usage: build-android.sh <arm|arm64|x86_64>
#
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ARCH="${1:?arch required: arm|arm64|x86_64}"
API="${ANDROID_API:-21}"

case "${ARCH}" in
  arm)    TARGET="armv7a-linux-androideabi"; TRIPLET="arm-linux-androideabi";  RID="android-arm";   ABI="armeabi-v7a"; CIJOB="android-arm" ;;
  arm64)  TARGET="aarch64-linux-android";    TRIPLET="aarch64-linux-android";  RID="android-arm64"; ABI="arm64-v8a";   CIJOB="android-arm64" ;;
  x86_64) TARGET="x86_64-linux-android";     TRIPLET="x86_64-linux-android";   RID="android-x64";   ABI="x86_64";      CIJOB="android-x86_64" ;;
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

# ---- 1. contribs: prebuilt bundle (fallback: source) -------------------------
CONTRIB_ENV=(HAVE_ANDROID=1 ANDROID_API="${API}" ANDROID_ABI="${ABI}" ANDROID_NDK="${ANDROID_NDK_HOME}")
contrib_prebuilt_or_build "${VLC_SRC}" "${TRIPLET}" "${CIJOB}"
CONTRIB_PREFIX="${VLC_SRC}/contrib/${TRIPLET}"

# ---- 2. bootstrap + configure ------------------------------------------------
( cd "${VLC_SRC}" && ./bootstrap )

BUILD_DIR="${VLC_SRC}/build-android-${ARCH}"
mkdir -p "${BUILD_DIR}"

CONFIG_FLAGS=(
  "--prefix=${INSTALL_PREFIX}"
  "--host=${TRIPLET}"
  "--with-contrib=${CONTRIB_PREFIX}"
  --disable-vlc --disable-qt --disable-skins2 --disable-nls
  --disable-lua --disable-a52
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

if [ "${ARCH}" = "arm" ]; then
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
cp -a "${VLC_SRC}/COPYING"     "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
cp -a "${VLC_SRC}/COPYING.LIB" "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
