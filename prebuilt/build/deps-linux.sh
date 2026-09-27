#!/usr/bin/env bash
#
# Installs the Debian/Ubuntu build toolchain plus the prebuilt distro
# dependencies used by build-linux.sh. Works both as root (inside an emulated
# arm container) and as a normal CI user (via sudo).
#
set -euo pipefail
SUDO=""
[ "$(id -u)" -eq 0 ] || SUDO="sudo"
export DEBIAN_FRONTEND=noninteractive

${SUDO} apt-get update
${SUDO} apt-get install -y --no-install-recommends \
  build-essential automake autoconf autopoint libtool-bin pkg-config gettext \
  git bison flex nasm yasm cmake ninja-build meson curl xz-utils zstd ca-certificates \
  gperf help2man patchelf \
  libavcodec-dev libavformat-dev libavutil-dev libswscale-dev \
  libfaad-dev libflac-dev libmad0-dev libmpcdec-dev libtheora-dev libvpx-dev \
  libogg-dev libopus-dev libmatroska-dev libebml-dev libdvbpsi-dev \
  libass-dev libfreetype6-dev libfontconfig1-dev libharfbuzz-dev libfribidi-dev \
  liborc-0.4-dev libxml2-dev
