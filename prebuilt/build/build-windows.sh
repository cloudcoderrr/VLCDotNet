#!/usr/bin/env bash
#
# Build libvlc 3.0.x for Windows (x64 / arm64) with llvm-mingw (UCRT).
#
# VideoLAN's published Windows prebuilt contrib bundles target either a
# gcc/msvcrt toolchain (win64) or the UWP LLVM image, neither of which links
# cleanly against a desktop UCRT/llvm-mingw libvlc. We therefore build the
# contrib closure from source here (VLC's win32 build.sh handles it), matching
# the module coverage of the from-source pipeline. The contrib set is the
# test-required codec closure; the full plugin tree is emitted as usual.
#
#   Usage: build-windows.sh <x86_64|aarch64>
#
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ARCH="${1:?arch required: x86_64 | aarch64}"
case "${ARCH}" in
  x86_64)  RID="win-x64"   ;;
  aarch64) RID="win-arm64" ;;
  *) die "Unsupported Windows arch: ${ARCH} (expected x86_64 or aarch64)" ;;
esac

VLC_SRC="${WORK_DIR}/vlc-win-${ARCH}"
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

# -r release, -z libvlc-only (no GUI), -u UCRT, -S 0x0A000000 target Win10 API,
# -a arch, -o install prefix. (No -p: contribs are built from source here.)
BUILD_ARGS=(-r -z -u -S 0x0A000000 -a "${ARCH}")

# Keep unrelated network/disc/UI modules out. Codec modules auto-enable from the
# contrib set below.
export CONFIGFLAGS="${CONFIGFLAGS:-} --disable-lua --disable-live555 --disable-realrtsp --disable-goom --disable-libcddb --disable-shout --disable-zvbi --disable-dvdread --disable-dvdnav --disable-nls --disable-update-check --disable-bluray --disable-gnutls --disable-srt --disable-aribcam --disable-fontconfig"

# Test-required codec closure (matches the from-source pipeline's coverage).
export CONTRIBFLAGS="${CONTRIBFLAGS:-} --disable-all --enable-ffmpeg --enable-faad2 --enable-flac --enable-mad --enable-mpcdec --enable-opus --enable-ogg --enable-matroska --enable-schroedinger --enable-sidplay2 --enable-theora --enable-vpx --enable-dvbpsi --enable-ass --disable-net --disable-disc"

EXTRA_LINK_FLAGS=""
if [ "${ARCH}" = "x86_64" ]; then
  # x86_64 UCRT contribs (freetype, SDL_image) emit _setjmp references; resolve
  # them via mingw-w64's support library at cross-link time only.
  EXTRA_LINK_FLAGS="-lmingwex"
fi

INSTALL_PREFIX="${WORK_DIR}/install-win-${ARCH}"
rm -rf "${INSTALL_PREFIX}"
mkdir -p "${INSTALL_PREFIX}"
BUILD_ARGS+=(-o "${INSTALL_PREFIX}")

# Wrap the gcc/g++ names build.sh invokes so they resolve to llvm-mingw clang and
# downgrade VLC 3's clang-default-error diagnostics. Only the cross compilers are
# affected; native tools used by build.sh's helper stage stay untouched.
WRAP_DIR="${WORK_DIR}/xwrap-${ARCH}"
mkdir -p "${WRAP_DIR}"
LENIENT="-Wno-error=incompatible-function-pointer-types -Wno-error=incompatible-pointer-types -Wno-error=implicit-function-declaration -Wno-error=int-conversion"
for pair in "gcc:clang" "g++:clang++" "clang:clang" "clang++:clang++" ; do
  name="${pair%%:*}"; realt="${pair##*:}"
  cat > "${WRAP_DIR}/${ARCH}-w64-mingw32-${name}" <<EOF
#!/bin/sh
extra_link_flags=""
for arg in "\$@"; do
  case "\$arg" in
    -c|-E|-S)
      exec "${LLVM_MINGW_DIR}/bin/${ARCH}-w64-mingw32-${realt}" ${LENIENT} "\$@"
      ;;
  esac
done
if [ -n "${EXTRA_LINK_FLAGS}" ]; then
  extra_link_flags="${EXTRA_LINK_FLAGS}"
fi
exec "${LLVM_MINGW_DIR}/bin/${ARCH}-w64-mingw32-${realt}" ${LENIENT} "\$@" \$extra_link_flags
EOF
  chmod +x "${WRAP_DIR}/${ARCH}-w64-mingw32-${name}"
done
export PATH="${WRAP_DIR}:${PATH}"

# libvlc only: provide a no-op makensis so build.sh's package step never tries
# to build the NSIS installer.
sudo tee /usr/local/bin/makensis >/dev/null <<'EOF'
#!/bin/sh
exit 0
EOF
sudo chmod +x /usr/local/bin/makensis

log "Running extras/package/win32/build.sh ${BUILD_ARGS[*]}"
(
  cd "${VLC_SRC}"
  ./extras/package/win32/build.sh "${BUILD_ARGS[@]}"
)

normalize_output "${RID}" "${INSTALL_PREFIX}"
