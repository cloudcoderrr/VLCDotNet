#!/usr/bin/env bash
#
# Installs the Debian/Ubuntu build toolchain used by the cached pipeline's
# build-linux.sh. The cached pipeline builds the ENTIRE dependency closure from
# source via VLC's contrib system, so NO codec/demux/subtitle -dev packages are
# installed here - only the compilers and build tools the contrib + libvlc build
# require. Works both as root (inside an emulated arm container) and as a normal
# CI user (via sudo).
#
set -euo pipefail
SUDO=""
[ "$(id -u)" -eq 0 ] || SUDO="sudo"
export DEBIAN_FRONTEND=noninteractive

${SUDO} apt-get update
${SUDO} apt-get install -y --no-install-recommends \
  build-essential automake autoconf autopoint libtool-bin pkg-config gettext \
  git bison flex nasm yasm cmake ninja-build meson curl wget xz-utils zstd ca-certificates \
  gperf help2man patchelf python3 python3-setuptools ragel \
  ant default-jdk-headless unzip zip

# Cross toolchains for building the arm/arm64 contribs on an x86_64 runner. The
# cached pipeline cross-compiles the linux arm/arm64 CONTRIB phase (a full
# closure under QEMU emulation is far too slow); QEMU is used only for the
# lighter libvlc phase. These packages exist only for an x86_64 host.
if [ "$(uname -m)" = "x86_64" ]; then
  ${SUDO} apt-get install -y --no-install-recommends \
    gcc-aarch64-linux-gnu g++-aarch64-linux-gnu libc6-dev-arm64-cross \
    gcc-arm-linux-gnueabihf g++-arm-linux-gnueabihf libc6-dev-armhf-cross || true
fi
