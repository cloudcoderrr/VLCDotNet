#!/usr/bin/env bash
#
# Build libvlc for Apple platforms (macOS / iOS / iOS simulator / Mac Catalyst)
# against the FULL VLC contrib set, built in-CI with the Xcode toolchain and
# cached in-repo (see cached/build/lib.sh). macOS ships a dylib; iOS / simulator
# / Catalyst statically link libvlc and stage a vlc_static_modules[] table.
#
# Two phases, selected by CACHED_PHASE:
#   CACHED_PHASE=contrib  build-apple.sh <platform> <arch>   -> cache contribs
#   build-apple.sh <platform> <arch>          (default libvlc) -> build libvlc
#
#   platform: macos | ios | iossimulator | maccatalyst
#   arch:     x86_64 | arm64
#
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

PLATFORM="${1:?platform required: macos|ios|iossimulator|maccatalyst}"
ARCH="${2:?arch required: x86_64|arm64}"
PHASE="${CACHED_PHASE:-libvlc}"

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

VLC_SRC="${WORK_DIR}/vlc-apple-${PLATFORM}-${ARCH}"
INSTALL_PREFIX="${WORK_DIR}/install-apple-${PLATFORM}-${ARCH}"
mkdir -p "${WORK_DIR}"

clone_vlc "${VLC_SRC}"
apply_patches "${VLC_SRC}"

# ---- toolchain / SDK environment --------------------------------------------
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
# VLC 4.0's older contrib sources trip modern clang's stricter defaults (e.g.
# libgcrypt calls getentropy() with no visible declaration). The symbols exist on
# the target at runtime (iOS 12+/macOS), so downgrade the new -Werror defaults to
# warnings for the contrib build, mirroring the Android pipeline. VLC 3 unaffected.
APPLE_LENIENT=""
[ "${VLC_SERIES}" != "3" ] && APPLE_LENIENT=" -Wno-error=implicit-function-declaration -Wno-error=incompatible-pointer-types -Wno-error=int-conversion"
export CFLAGS="${APPLE_CFLAGS}${APPLE_LENIENT}"
export CXXFLAGS="${APPLE_CFLAGS}"
export OBJCFLAGS="${APPLE_CFLAGS}${APPLE_LENIENT}"
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

# VLC 4.0's contrib sets CMAKE_SYSTEM_NAME=iOS for Catalyst (HAVE_IOS is set),
# but Catalyst builds against the macOS SDK, which CMake's iOS platform init
# rejects. Force Darwin on the contrib make so CMake treats it as macOS; the
# compiler still targets the macabi ABI through CFLAGS.
CONTRIB_MK=()
if [ "${VLC_SERIES}" != "3" ] && [ "${PLATFORM}" = "maccatalyst" ]; then
  CONTRIB_MK=(CMAKE_SYSTEM_NAME=Darwin)
fi

# VLC 4.0's x265 (and other cmake) contrib rules expand
#   -DASM_FLAGS="-mmacosx-version-min=$(VLC_DEPLOYMENT_TARGET);-isysroot;..."
# but our bootstrap leaves VLC_DEPLOYMENT_TARGET empty, so clang rejects the
# resulting "-mmacosx-version-min=" ("invalid version number"). Pass it as a
# make variable (highest precedence), parsed from MINVER, so the aarch64
# assembler gets a valid deployment target. x264 is unaffected (autotools, uses
# CFLAGS); iOS/simulator disable x265 and Catalyst prunes it (its deployment
# target lives in the -target macabi triple), so seeding it here is harmless.
case "${MINVER}" in
  *version-min=*) CONTRIB_MK+=(VLC_DEPLOYMENT_TARGET="${MINVER##*=}") ;;
esac

# Packages in the full default set that do not cross-build cleanly for a given
# Apple sub-target on the GitHub runners. Pruned from the contrib bootstrap only
# (the libvlc build autodetects whatever is present). Extend as CI reveals more.
#
# Apple uses the built-in securetransport TLS module, so the gnutls contrib and
# its crypto deps (nettle/gmp/gcrypt/gpg-error) are not needed on any Apple
# target (and gpg-error's mkheader OOMs under the iOS cross build). The
# crypto-dependent net packages (srt/ssh2/vncclient/shout) are dropped with them.
# The rest of the net group (live555/librist/smb2/microdns/nfs/upnp) builds and
# ships, so RTSP/RIST/SMB/mDNS-Bonjour/NFS/UPnP all work on Apple over http(s)
# served by securetransport. sparkle (the macOS auto-updater framework) is also
# dropped: libvlc builds --disable-sparkle, and its legacy build system fails on
# the current macOS SDK.
APPLE_PRUNE=(--disable-gnutls --disable-nettle --disable-gmp --disable-gcrypt --disable-gpg-error --disable-srt --disable-ssh2 --disable-vncclient --disable-shout --disable-sparkle)
case "${PLATFORM}" in
  ios|iossimulator)
    # Mobile ships no optical-disc hardware, so drop the disc stack (desktop-only).
    APPLE_PRUNE+=(--disable-disc --disable-schroedinger --disable-vpx --disable-ass --disable-harfbuzz --disable-fribidi)
    # VLC 4.0 only: asdcplib (Digital Cinema) is not a required module, and VLC 4's
    # x265 cmake passes an empty -mmacosx-version-min= to the iOS assembler, so
    # drop the encoders on VLC 4 mobile (VideoToolbox provides H.264/H.265 encode).
    [ "${VLC_SERIES}" != "3" ] && APPLE_PRUNE+=(--disable-asdcplib --disable-x264 --disable-x265) ;;
  maccatalyst)
    # Catalyst requires only avcodec+sout+videotoolbox; libvpx's VP9 RTC C++ does
    # not compile for the macabi arm64 target, and schroedinger/orc are unstable.
    # Catalyst uses the iOS API family, so the optical-disc stack is dropped too.
    # x264/x265's asm objects are tagged 'macOS' (not 'macabi'), so their static
    # libs fail to link into a Catalyst app; H.264/H.265 ENCODE on Catalyst uses
    # VideoToolbox instead.
    APPLE_PRUNE+=(--disable-disc --disable-x264 --disable-x265 --disable-schroedinger --disable-vpx)
    # VLC 4.0 Catalyst arm64 only: mpg123's NEON64 asm (dct36_neon64.S) is
    # assembled with a 'macOS' platform tag under the macabi cross, so it cannot
    # link into the Catalyst arm64 dynamic framework. MP3 decode falls back to
    # mad + avcodec (both retained). Catalyst x64 has no NEON64 asm and keeps it.
    { [ "${VLC_SERIES}" != "3" ] && [ "${ARCH}" = "arm64" ]; } && APPLE_PRUNE+=(--disable-mpg123) ;;
esac

# macOS host autoreconf needs gettext's iconv.m4 (AM_ICONV) and libtool's m4 on
# aclocal's search path, but Homebrew installs them in keg-only prefixes not
# searched by default. Add them so full-set packages (libcddb, ...) autoreconf.
if command -v brew >/dev/null 2>&1; then
  for _p in gettext libtool automake autoconf-archive; do
    _d="$(brew --prefix "${_p}" 2>/dev/null)/share/aclocal"
    [ -d "${_d}" ] && export ACLOCAL_PATH="${_d}:${ACLOCAL_PATH:-}"
  done
  unset _p _d
fi

# ---- Phase 1: build + cache the full contrib set ----------------------------
if [ "${PHASE}" = "contrib" ]; then
  PRUNE=()
  [ "${#APPLE_PRUNE[@]}" -gt 0 ] && PRUNE+=("${APPLE_PRUNE[@]}")
  [ -n "${CACHED_CONTRIB_PRUNE:-}" ] && PRUNE+=(${CACHED_CONTRIB_PRUNE})
  if [ -n "${CONTRIB_GROUP:-}" ]; then
    if [ "${#PRUNE[@]}" -gt 0 ]; then
      build_contrib_group "${VLC_SRC}" "contrib-apple-${PLATFORM}-${ARCH}" "${TRIPLET}" "${RID}" "${CONTRIB_GROUP}" -- "${PRUNE[@]}"
    else
      build_contrib_group "${VLC_SRC}" "contrib-apple-${PLATFORM}-${ARCH}" "${TRIPLET}" "${RID}" "${CONTRIB_GROUP}"
    fi
  else
    if [ "${#PRUNE[@]}" -gt 0 ]; then
      build_full_contrib "${VLC_SRC}" "contrib-apple-${PLATFORM}-${ARCH}" "${TRIPLET}" -- "${PRUNE[@]}"
    else
      build_full_contrib "${VLC_SRC}" "contrib-apple-${PLATFORM}-${ARCH}" "${TRIPLET}"
    fi
    pack_contrib "${VLC_SRC}" "${TRIPLET}" "${RID}"
  fi
  exit 0
fi

# ---- Phase 2: restore contribs + build libvlc -------------------------------
restore_contrib "${VLC_SRC}" "${TRIPLET}" "${RID}"
CONTRIB_PREFIX="${VLC_SRC}/contrib/${TRIPLET}"

( cd "${VLC_SRC}" && ./bootstrap )

BUILD_DIR="${VLC_SRC}/build-apple-${PLATFORM}-${ARCH}"
rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}"

CONFIG_FLAGS=(
  "--prefix=${INSTALL_PREFIX}"
  "--host=${TRIPLET}"
  "--with-contrib=${CONTRIB_PREFIX}"
  --disable-vlc --disable-qt --disable-skins2 --disable-macosx
  --disable-nls --disable-lua --disable-a52 --disable-sparkle
  --disable-bluray --disable-libxml2
  --disable-screen --disable-vcd
  # TLS via the built-in securetransport module (no gnutls contrib on Apple); srt
  # is pruned from the Apple contrib (needs a crypto backend we do not ship here).
  --disable-gnutls --disable-srt
  # Native transport/network stack: RTSP (live555) builds from the contrib net
  # group; RIST/SMB2/mDNS-Bonjour/NFS/UPnP auto-enable from pkg-config; http/https
  # + native HLS/DASH use securetransport for TLS.
  --enable-live555
  --enable-taglib        # metadata read/write (feature parity, contrib-provided)
  --enable-avcodec --enable-swscale
)
case "${PLATFORM}" in
  ios|iossimulator) CONFIG_FLAGS+=(--disable-fribidi --disable-harfbuzz --disable-libass --disable-macosx-avfoundation) ;;
  maccatalyst)      CONFIG_FLAGS+=(--disable-macosx-avfoundation) ;;
esac
# macOS is a reference-supported desktop: enable the local optical-disc stack and
# libxml2 (the mobile disc stack was pruned from their contrib closures). Later
# --enable flags win over the base --disable-bluray/--disable-libxml2 above.
case "${PLATFORM}" in
  macos) CONFIG_FLAGS+=(--enable-bluray --enable-dvdread --enable-dvdnav --enable-libxml2) ;;
esac
case "${PLATFORM}" in
  ios|iossimulator|maccatalyst) CONFIG_FLAGS+=(--enable-static --disable-shared --enable-static-modules) ;;
  macos)                        CONFIG_FLAGS+=(--enable-shared --disable-static) ;;
esac

CONFIGURE_ENV=("${CONTRIB_ENV[@]}" "PKG_CONFIG_LIBDIR=${CONTRIB_PREFIX}/lib/pkgconfig")

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
      "${CONTRIB_PREFIX}/lib" \
      "${MINVER} -arch ${VLCARCH} -isysroot ${SDKROOT} ${EXTRA_CFLAGS}" \
      "${VLCARCH}"
    # Dynamic-linkage delivery: link the staged archives + module table into a
    # single dynamic VLCDotNet.framework (parity scope forbids app-linked
    # archives / libvlc __Internal). Fatal: validated end-to-end on the iOS
    # simulator (framework embeds, signs, loads and registers every module).
    stage_dynamic_vlc "${RID}" "${INSTALL_PREFIX}" "${PLATFORM}" \
      "${MINVER}" "${VLCARCH}" "${SDKROOT}" "${EXTRA_CFLAGS}"
    ;;
esac
cp -a "${VLC_SRC}/COPYING"     "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
cp -a "${VLC_SRC}/COPYING.LIB" "${ARTIFACTS_DIR}/${RID}/" 2>/dev/null || true
