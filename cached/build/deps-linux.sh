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

# Feature-parity: system development libraries for the native Linux output,
# audio and hardware-decode backends libvlc enables (X11/XCB, OpenGL/EGL/GLES,
# Wayland, ALSA, PulseAudio, VA-API, VDPAU). These are linked by the libvlc
# plugins, NOT the contrib set, so they are not part of the self-contained
# contrib closure. Without them configure silently drops the display/audio/hwdec
# modules, and VLC hard-errors on xcb-composite once base xcb is detected.
${SUDO} apt-get install -y --no-install-recommends \
  libasound2-dev libpulse-dev \
  libx11-dev libx11-xcb-dev libxext-dev libxrender-dev \
  libxcb1-dev libxcb-shm0-dev libxcb-composite0-dev libxcb-xfixes0-dev \
  libxcb-randr0-dev libxcb-keysyms1-dev libxcb-xv0-dev libxcb-xkb-dev \
  libgl1-mesa-dev libegl1-mesa-dev libgles2-mesa-dev \
  libwayland-dev wayland-protocols libxkbcommon-dev \
  libva-dev libvdpau-dev

# Cross toolchains for building the arm/arm64 contribs on an x86_64 runner. The
# cached pipeline cross-compiles the linux arm/arm64 CONTRIB phase (a full
# closure under QEMU emulation is far too slow); QEMU is used only for the
# lighter libvlc phase. These packages exist only for an x86_64 host.
if [ "$(uname -m)" = "x86_64" ]; then
  ${SUDO} apt-get install -y --no-install-recommends \
    gcc-aarch64-linux-gnu g++-aarch64-linux-gnu libc6-dev-arm64-cross \
    gcc-arm-linux-gnueabihf g++-arm-linux-gnueabihf libc6-dev-armhf-cross || true
fi
