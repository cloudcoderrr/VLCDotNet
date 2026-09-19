#!/usr/bin/env bash
#
# Shared helpers for building libvlc 3.0.x from VideoLAN source.
#
set -euo pipefail

# The VLC tag to build. Kept in sync with <VlcVersion> in Directory.Build.props.
VLC_VERSION="${VLC_VERSION:-3.0.23}"
VLC_GIT="${VLC_GIT:-https://code.videolan.org/videolan/vlc.git}"

BUILD_LIB_DIR="$( cd "$(dirname "${BASH_SOURCE[0]}")" && pwd )"
REPO_ROOT="$( cd "${BUILD_LIB_DIR}/.." && pwd )"
PATCH_DIR="${REPO_ROOT}/patches/vlc-3.0"
WORK_DIR="${WORK_DIR:-${REPO_ROOT}/build/_work}"
ARTIFACTS_DIR="${ARTIFACTS_DIR:-${REPO_ROOT}/artifacts}"

log()  { printf '\033[1;32m[vlcdotnet]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[vlcdotnet]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[vlcdotnet]\033[0m %s\n' "$*" >&2; exit 1; }

# clone_vlc <dest>
# Shallow-clones the pinned VLC tag if not already present.
clone_vlc() {
  local dest="$1"
  if [ ! -d "${dest}/.git" ]; then
    log "Cloning VLC ${VLC_VERSION} -> ${dest}"
    git clone --depth 1 --branch "${VLC_VERSION}" "${VLC_GIT}" "${dest}"
  else
    log "Reusing existing VLC checkout at ${dest}"
    git -C "${dest}" reset --hard HEAD
    git -C "${dest}" clean -fdx
  fi
}

# apply_patches <vlc-src>
# Applies every patches/vlc-3.0/*.patch in lexical order. Uses git apply
# --recount (tolerant of hunk line-count/offset drift) with a patch -p1 fallback.
apply_patches() {
  local src="$1"
  shopt -s nullglob
  local applied=0
  for p in "${PATCH_DIR}"/*.patch; do
    log "Applying patch $(basename "$p")"
    if git -C "${src}" apply --recount --whitespace=nowarn "$p" 2>/dev/null; then
      :
    elif ( cd "${src}" && patch -p1 --forward --fuzz=3 --no-backup-if-mismatch < "$p" ); then
      :
    else
      die "Patch $(basename "$p") failed to apply"
    fi
    applied=$((applied + 1))
  done
  shopt -u nullglob
  log "Applied ${applied} patch(es)"
}

# normalize_output <rid> <install-prefix>
# Copies the built libvlc runtime (libs + plugins + licenses) from an installed
# prefix into artifacts/<rid>/ using a stable layout consumed by the packer.
normalize_output() {
  local rid="$1" prefix="$2"
  local out="${ARTIFACTS_DIR}/${rid}"
  rm -rf "${out}"
  mkdir -p "${out}"

  log "Collecting ${rid} runtime from ${prefix}"

  # Shared libraries (search both lib/ and bin/ for cross layouts).
  # -L dereferences the libvlc.so -> libvlc.so.5 -> libvlc.so.5.x symlink chains
  # into real files, because CI artifact upload does not preserve symlinks.
  local f
  for f in \
    "${prefix}"/libvlc*.dll "${prefix}"/libvlccore*.dll \
    "${prefix}"/bin/libvlc*.dll "${prefix}"/bin/libvlccore*.dll \
    "${prefix}"/lib/libvlc*.so* "${prefix}"/lib/libvlccore*.so* \
    "${prefix}"/lib/libvlc*.dylib "${prefix}"/lib/libvlccore*.dylib \
    "${prefix}"/lib/libvlc*.a "${prefix}"/lib/libvlccore*.a ; do
    [ -e "$f" ] && cp -aL "$f" "${out}/" || true
  done

  # Plugins tree (location differs per platform).
  local plugins=""
  for cand in \
    "${prefix}/lib/vlc/plugins" \
    "${prefix}/plugins" \
    "${prefix}/lib/plugins" ; do
    if [ -d "${cand}" ]; then plugins="${cand}"; break; fi
  done
  if [ -n "${plugins}" ]; then
    mkdir -p "${out}/plugins"
    # Copy only the plugin shared objects, preserving the module folder layout.
    ( cd "${plugins}" && find . \( -name '*.dll' -o -name '*.so' -o -name '*.dylib' \) -print0 \
        | while IFS= read -r -d '' m; do
            mkdir -p "${out}/plugins/$(dirname "$m")"
            cp -a "$m" "${out}/plugins/$m"
          done )
  else
    warn "No plugins directory found under ${prefix}"
  fi

  # License files for redistribution compliance.
  for l in COPYING COPYING.LIB ; do
    [ -f "${prefix}/../${l}" ] && cp -a "${prefix}/../${l}" "${out}/" || true
  done

  log "Wrote ${rid} -> ${out}"
  ( cd "${out}" && find . -maxdepth 2 -type f | sort | head -n 40 )
}

# jobs
# Number of parallel make jobs.
jobs() { getconf _NPROCESSORS_ONLN 2>/dev/null || echo 2; }
