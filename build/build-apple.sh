#!/usr/bin/env bash
#
# Build libvlc for Apple platforms using the VLC contrib system with the Xcode
# toolchain. Runs on a macOS runner.
#
#   Usage: build-apple.sh <macos|ios|iossimulator|maccatalyst> <x86_64|arm64>
#
# NOTE: Apple cross builds are the most sensitive part of the pipeline; this
# script encodes the documented contrib + configure flow and is expected to be
# iterated against CI logs.
#
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

PLATFORM="${1:?platform required: macos|ios|iossimulator|maccatalyst}"
ARCH="${2:?arch required: x86_64|arm64}"

case "${ARCH}" in
  x86_64) VLCARCH="x86_64"; TRIPLET="x86_64-apple-darwin" ;;
  arm64)  VLCARCH="arm64";  TRIPLET="aarch64-apple-darwin" ;;
  *) die "Unsupported Apple arch: ${ARCH}" ;;
esac

EXTRA_CFLAGS=""
case "${PLATFORM}" in
  macos)
    SDK="macosx";          RID="osx-${ARCH/x86_64/x64}"
    MINVER="-mmacosx-version-min=10.11" ;;
  ios)
    SDK="iphoneos";        RID="ios-${ARCH}"
    MINVER="-miphoneos-version-min=12.0" ;;
  iossimulator)
    SDK="iphonesimulator"; RID="iossimulator-${ARCH/x86_64/x64}"
    MINVER="-mios-simulator-version-min=12.0" ;;
  maccatalyst)
    SDK="macosx";          RID="maccatalyst-${ARCH/x86_64/x64}"
    MINVER=""; EXTRA_CFLAGS="-target ${VLCARCH}-apple-ios13.1-macabi" ;;
  *) die "Unsupported Apple platform: ${PLATFORM}" ;;
esac
RID="${RID/arm64/arm64}"

VLC_SRC="${WORK_DIR}/vlc-apple-${PLATFORM}-${ARCH}"
INSTALL_PREFIX="${WORK_DIR}/install-apple-${PLATFORM}-${ARCH}"
mkdir -p "${WORK_DIR}"

clone_vlc "${VLC_SRC}"
apply_patches "${VLC_SRC}"

SDKROOT="$(xcrun --sdk "${SDK}" --show-sdk-path)"
export SDKROOT
export CC="xcrun --sdk ${SDK} clang"
export CXX="xcrun --sdk ${SDK} clang++"
export OBJC="xcrun --sdk ${SDK} clang"
export AR="$(xcrun --sdk ${SDK} --find ar)"
export RANLIB="$(xcrun --sdk ${SDK} --find ranlib)"
export STRIP="$(xcrun --sdk ${SDK} --find strip)"
export NM="$(xcrun --sdk ${SDK} --find nm)"

APPLE_CFLAGS="-arch ${VLCARCH} -isysroot ${SDKROOT} ${MINVER} ${EXTRA_CFLAGS}"
export CFLAGS="${APPLE_CFLAGS}"
export CXXFLAGS="${APPLE_CFLAGS}"
export OBJCFLAGS="${APPLE_CFLAGS}"
export LDFLAGS="-arch ${VLCARCH} -isysroot ${SDKROOT} ${MINVER} ${EXTRA_CFLAGS}"

# Contrib environment flags used by contrib/src/main.mak to select the platform.
CONTRIB_ENV=()
case "${PLATFORM}" in
  ios|iossimulator) CONTRIB_ENV=(HAVE_IOS=1 HAVE_DARWIN_OS=1) ;;
  maccatalyst)      CONTRIB_ENV=(HAVE_MACCATALYST=1 HAVE_DARWIN_OS=1) ;;
  macos)            CONTRIB_ENV=(HAVE_MACOSX=1 HAVE_DARWIN_OS=1) ;;
esac

# ---- 1. contribs --------------------------------------------------------------
CONTRIB_BUILD="${VLC_SRC}/contrib/contrib-apple-${PLATFORM}-${ARCH}"
mkdir -p "${CONTRIB_BUILD}"
(
  cd "${CONTRIB_BUILD}"
  env "${CONTRIB_ENV[@]}" ../bootstrap --host="${TRIPLET}" --disable-gpl --disable-disc --disable-gettext
  if ! env "${CONTRIB_ENV[@]}" make prebuilt 2>/dev/null; then
    warn "Prebuilt contribs unavailable for ${TRIPLET}/${PLATFORM}; building from source"
    env "${CONTRIB_ENV[@]}" make -j"$(jobs)" fetch
    env "${CONTRIB_ENV[@]}" make -j"$(jobs)" || env "${CONTRIB_ENV[@]}" make -j1
  fi
)

# ---- 2. bootstrap + configure -------------------------------------------------
( cd "${VLC_SRC}" && ./bootstrap )

BUILD_DIR="${VLC_SRC}/build-apple-${PLATFORM}-${ARCH}"
mkdir -p "${BUILD_DIR}"

CONFIG_FLAGS=(
  "--prefix=${INSTALL_PREFIX}"
  "--host=${TRIPLET}"
  "--with-contrib=${VLC_SRC}/contrib/${TRIPLET}"
  --disable-vlc --disable-qt --disable-skins2 --disable-macosx
  --disable-nls --disable-lua --disable-a52
  --enable-avcodec --enable-swscale
)
# Apple mobile / catalyst require static libvlc; macOS ships a dylib.
case "${PLATFORM}" in
  ios|iossimulator|maccatalyst)
    CONFIG_FLAGS+=(--enable-static --disable-shared --enable-static-modules) ;;
  macos)
    CONFIG_FLAGS+=(--enable-shared --disable-static) ;;
esac

(
  cd "${BUILD_DIR}"
  env "${CONTRIB_ENV[@]}" ../configure "${CONFIG_FLAGS[@]}"
  env "${CONTRIB_ENV[@]}" make -j"$(jobs)"
  make install
)

normalize_output "${RID}" "${INSTALL_PREFIX}"
cp -a "${VLC_SRC}/COPYING"     "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
cp -a "${VLC_SRC}/COPYING.LIB" "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
