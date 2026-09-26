#!/usr/bin/env bash
#
# Build libvlc 3.0.x for Apple platforms (macOS / iOS / iOS simulator /
# Mac Catalyst) with the Xcode toolchain. VideoLAN does not publish 3.0.x
# prebuilt contrib bundles that link against these SDKs/toolchains, so the
# contrib closure is built from source here (the test-required codec set),
# matching the from-source pipeline. macOS ships a dylib; iOS/simulator/
# Catalyst statically link libvlc and stage a vlc_static_modules[] table.
#
#   Usage: build-apple.sh <macos|ios|iossimulator|maccatalyst> <x86_64|arm64>
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

# --- inlined contrib/codec flag helpers (independent of the from-source lib) ---
minimal_contrib_flags() {
  local include_ass="${1:-1}"
  local flags=(
    --disable-all --enable-ffmpeg --enable-faad2 --enable-flac --enable-mad
    --enable-mpcdec --enable-opus --enable-ogg --enable-matroska
    --enable-schroedinger --enable-sidplay2 --enable-theora --enable-vpx
    --enable-dvbpsi --disable-net --disable-disc
  )
  [ "${include_ass}" = "1" ] && flags+=(--enable-ass)
  printf '%s\n' "${flags[@]}"
}
requested_codec_flags() {
  printf '%s\n' --enable-faad --enable-flac --enable-mad --enable-mpc \
    --enable-schroedinger --enable-sid --enable-theora --enable-vpx
}
strip_network_contribs() {
  local prefix="$1"
  rm -f "${prefix}"/lib/libgnutls* "${prefix}"/lib/pkgconfig/gnutls.pc \
        "${prefix}"/lib/libsrt*    "${prefix}"/lib/pkgconfig/srt.pc 2>/dev/null || true
}

VLC_SRC="${WORK_DIR}/vlc-apple-${PLATFORM}-${ARCH}"
INSTALL_PREFIX="${WORK_DIR}/install-apple-${PLATFORM}-${ARCH}"
mkdir -p "${WORK_DIR}"

clone_vlc "${VLC_SRC}"
apply_patches "${VLC_SRC}"

SDKROOT="$(xcrun --sdk "${SDK}" --show-sdk-path)"
export SDKROOT
CLANG="$(xcrun --sdk "${SDK}" --find clang)"
CLANGXX="$(xcrun --sdk "${SDK}" --find clang++)"
BUILD_SDKROOT="$(xcrun --sdk macosx --show-sdk-path)"
BUILD_CLANG="$(xcrun --sdk macosx --find clang)"
BUILD_CLANGXX="$(xcrun --sdk macosx --find clang++)"
export CC="${CLANG}"
export CXX="${CLANGXX}"
export OBJC="${CLANG}"
export AR="$(xcrun --sdk ${SDK} --find ar)"
export RANLIB="$(xcrun --sdk ${SDK} --find ranlib)"
export STRIP="$(xcrun --sdk ${SDK} --find strip)"
export NM="$(xcrun --sdk ${SDK} --find nm)"

BUILD_CFLAGS="-arch $(uname -m) -isysroot ${BUILD_SDKROOT}"
export CC_FOR_BUILD="${BUILD_CLANG}"
export CXX_FOR_BUILD="${BUILD_CLANGXX}"
export OBJC_FOR_BUILD="${BUILD_CLANG}"
export CPPFLAGS_FOR_BUILD=""
export CFLAGS_FOR_BUILD="${BUILD_CFLAGS}"
export CXXFLAGS_FOR_BUILD="${BUILD_CFLAGS}"
export LDFLAGS_FOR_BUILD="${BUILD_CFLAGS}"
export BUILDCC="${BUILD_CLANG} -isysroot ${BUILD_SDKROOT}"

if [ "${PLATFORM}" = "maccatalyst" ]; then
  EXTRA_CFLAGS="${EXTRA_CFLAGS} -iframework ${SDKROOT}/System/iOSSupport/System/Library/Frameworks -isystem ${SDKROOT}/System/iOSSupport/usr/include"
fi

APPLE_CFLAGS="-arch ${VLCARCH} -isysroot ${SDKROOT} ${MINVER} ${EXTRA_CFLAGS}"
export CFLAGS="${APPLE_CFLAGS}"
export CXXFLAGS="${APPLE_CFLAGS}"
export OBJCFLAGS="${APPLE_CFLAGS}"
export CPPFLAGS="${APPLE_CFLAGS}"
export LDFLAGS="-arch ${VLCARCH} -isysroot ${SDKROOT} ${MINVER} ${EXTRA_CFLAGS}"
if [ "${ARCH}" = "x86_64" ]; then
  export LDFLAGS="${LDFLAGS} -Wl,-ld_classic"
fi

CONTRIB_ENV=()
case "${PLATFORM}" in
  ios|iossimulator) CONTRIB_ENV=(BUILDFORIOS=1 HAVE_IOS=1 HAVE_DARWIN_OS=1 VLCSDKROOT="${SDKROOT}") ;;
  maccatalyst)      CONTRIB_ENV=(BUILDFORIOS=1 HAVE_MACCATALYST=1 HAVE_DARWIN_OS=1 VLCSDKROOT="${SDKROOT}") ;;
  macos)            CONTRIB_ENV=(HAVE_MACOSX=1 HAVE_DARWIN_OS=1) ;;
esac

# ---- 1. contribs (from source) ------------------------------------------------
CONTRIB_BUILD="${VLC_SRC}/contrib/contrib-apple-${PLATFORM}-${ARCH}"
mkdir -p "${CONTRIB_BUILD}"
BOOTSTRAP_INCLUDE_ASS=0
[ "${PLATFORM}" = "macos" ] && BOOTSTRAP_INCLUDE_ASS=1
BOOTSTRAP_FLAGS=($(minimal_contrib_flags "${BOOTSTRAP_INCLUDE_ASS}"))
CODEC_FLAGS=($(requested_codec_flags))
case "${PLATFORM}" in
  ios|iossimulator|maccatalyst)
    # libvpx RTC and schroedinger/orc are unstable in this cross flow and not
    # required for the mobile VT-decode + playback surface.
    FILTERED=(); for f in "${BOOTSTRAP_FLAGS[@]}"; do case "$f" in --enable-vpx|--enable-schroedinger) ;; *) FILTERED+=("$f");; esac; done; BOOTSTRAP_FLAGS=("${FILTERED[@]}")
    FILTERED=(); for f in "${CODEC_FLAGS[@]}";     do case "$f" in --enable-vpx|--enable-schroedinger) ;; *) FILTERED+=("$f");; esac; done; CODEC_FLAGS=("${FILTERED[@]}")
    ;;
esac
(
  cd "${CONTRIB_BUILD}"
  env "${CONTRIB_ENV[@]}" ../bootstrap --host="${TRIPLET}" "${BOOTSTRAP_FLAGS[@]}"
  env "${CONTRIB_ENV[@]}" make -j"$(jobs)" fetch
  env "${CONTRIB_ENV[@]}" make -j"$(jobs)" || env "${CONTRIB_ENV[@]}" make -j1
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
  --disable-nls --disable-lua --disable-a52 --disable-sparkle
  --disable-bluray --disable-gnutls --disable-srt --disable-libxml2
  --disable-screen --disable-vcd --disable-live555 --disable-realrtsp
  --enable-avcodec --enable-swscale
)
CONFIG_FLAGS+=("${CODEC_FLAGS[@]}")
case "${PLATFORM}" in
  ios|iossimulator) CONFIG_FLAGS+=(--disable-fribidi --disable-harfbuzz --disable-libass --disable-macosx-avfoundation) ;;
  maccatalyst)      CONFIG_FLAGS+=(--disable-macosx-avfoundation) ;;
esac
case "${PLATFORM}" in
  ios|iossimulator|maccatalyst) CONFIG_FLAGS+=(--enable-static --disable-shared --enable-static-modules) ;;
  macos)                        CONFIG_FLAGS+=(--enable-shared --disable-static) ;;
esac

CONFIGURE_ENV=("${CONTRIB_ENV[@]}" "PKG_CONFIG_LIBDIR=${VLC_SRC}/contrib/${TRIPLET}/lib/pkgconfig")
strip_network_contribs "${VLC_SRC}/contrib/${TRIPLET}"

(
  cd "${BUILD_DIR}"
  env "${CONFIGURE_ENV[@]}" ../configure "${CONFIG_FLAGS[@]}"
  env "${CONFIGURE_ENV[@]}" make -j"$(jobs)"
  make install
)

normalize_output "${RID}" "${INSTALL_PREFIX}"
case "${PLATFORM}" in
  ios|iossimulator|maccatalyst)
    export NM="$(xcrun --sdk "${SDK}" --find nm)"
    export LD="$(xcrun --sdk "${SDK}" --find ld-classic 2>/dev/null || xcrun --sdk "${SDK}" --find ld)"
    stage_static_vlc "${RID}" "${INSTALL_PREFIX}" \
      "${VLC_SRC}/contrib/${TRIPLET}/lib" \
      "${MINVER} -arch ${VLCARCH} -isysroot ${SDKROOT} ${EXTRA_CFLAGS}" \
      "${VLCARCH}"
    ;;
esac
cp -a "${VLC_SRC}/COPYING"     "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
cp -a "${VLC_SRC}/COPYING.LIB" "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
