#!/usr/bin/env bash
#
# Build libvlc for Android (arm / arm64 / x86_64) using the VLC contrib system
# with the Android NDK (unified clang toolchain). Runs on a Linux runner with
# ANDROID_NDK_HOME set.
#
#   Usage: build-android.sh <arm|arm64|x86_64>
#
# NOTE: Android from-source is iterated against CI logs; plugin flattening for
# the APK layout is handled later by the packer.
#
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ARCH="${1:?arch required: arm|arm64|x86_64}"
API="${ANDROID_API:-21}"

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
# Android's NDK clang treats several legacy C/C++ constructs in VLC 3's contrib
# stack as errors; downgrade the ones that are valid warnings under both gcc and
# clang so old third-party code still builds.
export CFLAGS="${CFLAGS:-} -Wno-error=implicit-function-declaration -Wno-error=incompatible-pointer-types -Wno-error=int-conversion"
export CXXFLAGS="${CXXFLAGS:-} -Wno-error=incompatible-pointer-types -Wno-c++11-narrowing"

VLC_SRC="${WORK_DIR}/vlc-android-${ARCH}"
INSTALL_PREFIX="${WORK_DIR}/install-android-${ARCH}"
mkdir -p "${WORK_DIR}"

clone_vlc "${VLC_SRC}"
apply_patches "${VLC_SRC}"

CONTRIB_ENV=(HAVE_ANDROID=1 ANDROID_API="${API}" ANDROID_ABI="${ABI}" ANDROID_NDK="${ANDROID_NDK_HOME}")

# ---- 1. contribs --------------------------------------------------------------
CONTRIB_BUILD="${VLC_SRC}/contrib/contrib-android-${ARCH}"
mkdir -p "${CONTRIB_BUILD}"
(
  cd "${CONTRIB_BUILD}"
  # Minimal contrib closure for local-file playback of the test media.
  env "${CONTRIB_ENV[@]}" ../bootstrap --host="${TRIPLET}" --disable-all --enable-ffmpeg --enable-opus --enable-ass --enable-matroska --disable-net --disable-sout --disable-disc
  prefetch_contrib_tarballs "${VLC_SRC}/contrib/tarballs"
  env "${CONTRIB_ENV[@]}" make -j"$(jobs)" fetch
  env "${CONTRIB_ENV[@]}" make -j"$(jobs)" || env "${CONTRIB_ENV[@]}" make -j1
)

# ---- 2. bootstrap + configure -------------------------------------------------
( cd "${VLC_SRC}" && ./bootstrap )

BUILD_DIR="${VLC_SRC}/build-android-${ARCH}"
mkdir -p "${BUILD_DIR}"

CONFIG_FLAGS=(
  "--prefix=${INSTALL_PREFIX}"
  "--host=${TRIPLET}"
  "--with-contrib=${VLC_SRC}/contrib/${TRIPLET}"
  --disable-vlc --disable-qt --disable-skins2 --disable-nls
  --disable-lua --disable-a52 --disable-sid
  --disable-vpx --disable-sdl-image
  --disable-bluray
  --disable-xcb --disable-alsa --disable-pulse --disable-vdpau
  --disable-v4l2 --disable-vnc --disable-gnutls --disable-srt --disable-ncurses
  --disable-libxml2
  --enable-avcodec --enable-swscale
  --enable-shared --disable-static
)

# Android API 21 ships a sys/shm.h header stub, but the SysV shared-memory
# functions (shmdt/shmctl/...) are only available on newer API levels. Force
# VLC's configure probe down the non-shm path so block.c uses its existing
# fallback implementation instead of compiling undeclared calls.
CONFIG_ENV=("${CONTRIB_ENV[@]}" ac_cv_header_sys_shm_h=no)

# Android only needs local-media playback for the current test suite. Removing
# gnutls/srt from the contrib prefix keeps VLC from linking optional desktop/
# network modules (notably VNC) against partial nettle backports on Android.
CONTRIB_PREFIX_DIR="${VLC_SRC}/contrib/${TRIPLET}"
rm -f "${CONTRIB_PREFIX_DIR}"/lib/libgnutls* \
  "${CONTRIB_PREFIX_DIR}"/lib/pkgconfig/gnutls.pc \
  "${CONTRIB_PREFIX_DIR}"/lib/libsrt* \
  "${CONTRIB_PREFIX_DIR}"/lib/pkgconfig/srt.pc 2>/dev/null || true

# NDK r29 clang no longer implicitly links libm into the module plugins, so math
# symbols (e.g. log10f in the audiotrack output) are unresolved. NDK r29 also
# folds pthread into libc and ships no libpthread.a, yet some modules link
# -lpthread explicitly (e.g. the adaptive demux). Provide -lm plus an empty
# libpthread.a stub so both resolve for every plugin link.
PTHREAD_STUB_DIR="${WORK_DIR}/pthread-stub-${ARCH}"
mkdir -p "${PTHREAD_STUB_DIR}"
"${AR}" rc "${PTHREAD_STUB_DIR}/libpthread.a"
export LDFLAGS="${LDFLAGS:-} -L${PTHREAD_STUB_DIR} -lm"

(
  cd "${BUILD_DIR}"
  env "${CONFIG_ENV[@]}" ../configure "${CONFIG_FLAGS[@]}"
  env "${CONFIG_ENV[@]}" make -j"$(jobs)"
  make install
)

normalize_output "${RID}" "${INSTALL_PREFIX}"
cp -a "${VLC_SRC}/COPYING"     "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
cp -a "${VLC_SRC}/COPYING.LIB" "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
