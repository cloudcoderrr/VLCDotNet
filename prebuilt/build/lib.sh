#!/usr/bin/env bash
#
# Shared helpers for building libvlc 3.0.x from VideoLAN source using
# *prebuilt* third-party dependencies ("contribs") instead of compiling the
# dependency closure from source.
#
# Strategy per platform:
#   * cross targets (Windows / Apple / Android): fetch VideoLAN's official
#     prebuilt contrib bundle via the VLC contrib `make prebuilt` target
#     (downloads vlc-contrib-<host>-latest.tar.zst and fixes its prefixes).
#     If no prebuilt bundle is published for the host triplet, transparently
#     fall back to a full from-source contrib build so the target still ships.
#   * Linux: use the distribution's prebuilt -dev packages (apt) for the whole
#     dependency set, plus a tiny source build for the few codecs Ubuntu no
#     longer packages (schroedinger, sidplay2).
#
# This is intentionally independent of build/lib.sh (the from-source pipeline)
# so the two build systems can evolve side by side.
#
set -euo pipefail

# The VLC tag to build. Kept in sync with <VlcVersion> in Directory.Build.props.
VLC_VERSION="${VLC_VERSION:-3.0.23}"
VLC_GIT="${VLC_GIT:-https://code.videolan.org/videolan/vlc.git}"

BUILD_LIB_DIR="$( cd "$(dirname "${BASH_SOURCE[0]}")" && pwd )"
REPO_ROOT="$( cd "${BUILD_LIB_DIR}/../.." && pwd )"
PATCH_DIR="${PATCH_DIR:-${REPO_ROOT}/prebuilt/patches/vlc-3.0}"
WORK_DIR="${WORK_DIR:-${REPO_ROOT}/prebuilt/build/_work}"
ARTIFACTS_DIR="${ARTIFACTS_DIR:-${REPO_ROOT}/artifacts}"

log()  { printf '\033[1;36m[vlcdotnet-prebuilt]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[vlcdotnet-prebuilt]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[vlcdotnet-prebuilt]\033[0m %s\n' "$*" >&2; exit 1; }

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
# Applies every prebuilt/patches/vlc-3.0/*.patch in lexical order.
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

# jobs
# Number of parallel make jobs.
jobs() { getconf _NPROCESSORS_ONLN 2>/dev/null || echo 2; }

# contrib_prebuilt_or_build <vlc-src> <triplet> [-- <bootstrap-flag>...]
#
# Prepares the contrib prefix at <vlc-src>/contrib/<triplet> using VideoLAN's
# prebuilt bundle when available, otherwise a full from-source contrib build.
# Reads an optional CONTRIB_ENV array from the caller's environment for the
# platform selector variables (HAVE_ANDROID=1, BUILDFORIOS=1, ...).
#
# Sets CONTRIB_MODE to "prebuilt" or "source" for the caller's logging.
CONTRIB_MODE=""
contrib_prebuilt_or_build() {
  local src="$1" triplet="$2"; shift 2
  local -a bootstrap_flags=()
  if [ "${1:-}" = "--" ]; then
    shift
    bootstrap_flags=("$@")
  fi
  local -a cenv=()
  if declare -p CONTRIB_ENV >/dev/null 2>&1; then
    cenv=("${CONTRIB_ENV[@]}")
  fi

  local cbuild="${src}/contrib/contrib-prebuilt-${triplet}"
  local cprefix="${src}/contrib/${triplet}"
  mkdir -p "${cbuild}"

  (
    cd "${cbuild}"
    env "${cenv[@]}" ../bootstrap --host="${triplet}" "${bootstrap_flags[@]}"
  )

  # Try the official prebuilt bundle first.
  if [ "${CONTRIB_FORCE_SOURCE:-0}" != "1" ] \
     && ( cd "${cbuild}" && env "${cenv[@]}" make prebuilt ) 2>&1 | tee "${WORK_DIR}/contrib-prebuilt-${triplet}.log"; then
    if [ -d "${cprefix}/lib" ] || [ -d "${cprefix}/lib64" ]; then
      CONTRIB_MODE="prebuilt"
      log "Contrib: using VideoLAN prebuilt bundle for ${triplet}"
      return 0
    fi
    warn "make prebuilt reported success but ${cprefix} looks empty; falling back to source"
  else
    warn "No prebuilt contrib bundle for ${triplet} (or download failed); building from source"
  fi

  # Fallback: full from-source contrib build (default package set).
  (
    cd "${cbuild}"
    env "${cenv[@]}" make -j"$(jobs)" fetch
    env "${cenv[@]}" make -j"$(jobs)" || env "${cenv[@]}" make -j1
  )
  CONTRIB_MODE="source"
  log "Contrib: built ${triplet} from source (fallback)"
}

# normalize_output <rid> <install-prefix>
# Copies the built libvlc runtime (libs + plugins + licenses) from an installed
# prefix into artifacts/<rid>/ using the stable layout consumed by the packer.
normalize_output() {
  local rid="$1" prefix="$2"
  local out="${ARTIFACTS_DIR}/${rid}"
  rm -rf "${out}"
  mkdir -p "${out}"

  log "Collecting ${rid} runtime from ${prefix}"

  local f
  for f in \
    "${prefix}"/libvlc*.dll "${prefix}"/libvlccore*.dll \
    "${prefix}"/bin/libvlc*.dll "${prefix}"/bin/libvlccore*.dll \
    "${prefix}"/lib/libvlc*.so* "${prefix}"/lib/libvlccore*.so* \
    "${prefix}"/lib/libvlc*.dylib "${prefix}"/lib/libvlccore*.dylib \
    "${prefix}"/lib/libvlc*.a "${prefix}"/lib/libvlccore*.a ; do
    [ -e "$f" ] && cp -aL "$f" "${out}/" || true
  done

  # Copy any extra top-level dependency shared libraries the runtime needs
  # (dynamic contrib builds can install ffmpeg/ass/opus/... next to libvlc).
  if [ -d "${prefix}/lib" ]; then
    ( cd "${prefix}/lib" && find . -maxdepth 1 -type f \( -name '*.so' -o -name '*.so.*' -o -name '*.dylib' -o -name '*.dll' \) -print0 \
        | while IFS= read -r -d '' m; do
            case "$(basename "$m")" in
              libvlc*.so|libvlc*.so.*|libvlccore*.so|libvlccore*.so.*|libvlc*.dylib|libvlccore*.dylib) continue ;;
            esac
            cp -aL "$m" "${out}/"
          done )
  fi

  # Plugins tree (location differs per platform).
  local plugins=""
  local cand
  for cand in \
    "${prefix}/lib/vlc/plugins" \
    "${prefix}/plugins" \
    "${prefix}/lib/plugins" ; do
    if [ -d "${cand}" ]; then plugins="${cand}"; break; fi
  done
  if [ -n "${plugins}" ]; then
    mkdir -p "${out}/plugins"
    ( cd "${plugins}" && find . \( -name '*.dll' -o -name '*.so' -o -name '*.dylib' \) -print0 \
        | while IFS= read -r -d '' m; do
            mkdir -p "${out}/plugins/$(dirname "$m")"
            cp -a "$m" "${out}/plugins/$m"
          done )
  else
    warn "No plugins directory found under ${prefix}"
  fi

  local l
  for l in COPYING COPYING.LIB ; do
    [ -f "${prefix}/../${l}" ] && cp -a "${prefix}/../${l}" "${out}/" || true
  done

  log "Wrote ${rid} -> ${out}"
  ( cd "${out}" && find . -maxdepth 2 -type f | sort | head -n 40 )
}

# stage_static_vlc <rid> <install-prefix> <contrib-lib-dir> <target-cflags> <arch>
# For static Apple builds (iOS / simulator / Mac Catalyst): collect the static
# plugin archives needed for local-file playback plus every contrib archive,
# then synthesise and compile a vlc_static_modules[] table into
# libvlcstaticmodules.a. Identical in spirit to the from-source pipeline.
stage_static_vlc() {
  local rid="$1" prefix="$2" contriblib="$3" cflags="$4" arch="$5"
  local out="${ARTIFACTS_DIR}/${rid}"
  local sdir="${out}/static"
  local nm="${NM:-nm}"
  mkdir -p "${sdir}"

  log "Staging static plugin + contrib archives for ${rid}"

  local syms="" a staged_modules=""
  local plugdir="${prefix}/lib/vlc/plugins"
  if [ -d "${plugdir}" ]; then
    while IFS= read -r -d '' a; do
      local base cat stage_module=0
      base="$(basename "$a" .a)"
      cat="$(basename "$(dirname "$a")")"
      case "${cat}" in
        access|audio_filter|audio_output|codec|demux|packetizer|spu|text_renderer|video_chroma|video_output)
          stage_module=1
          ;;
        access_output)
          case "${base}" in
            libaccess_output_file_plugin) stage_module=1 ;;
          esac
          ;;
        stream_out)
          case "${base}" in
            libstream_out_standard_plugin|libstream_out_transcode_plugin) stage_module=1 ;;
          esac
          ;;
      esac
      [ "${stage_module}" = "1" ] || continue
      cp -a "$a" "${sdir}/"
      staged_modules="${staged_modules}
$(printf '%s\n' "${base#lib}" | sed 's/_plugin$//')"
      syms="${syms}
$("${nm}" "$a" 2>/dev/null | grep -oE 'vlc_entry__[A-Za-z0-9_]+' | sort -u)"
    done < <(find "${plugdir}" -name '*.a' -print0)
  fi

  [ -f "${prefix}/lib/vlc/libcompat.a" ] && cp -a "${prefix}/lib/vlc/libcompat.a" "${sdir}/"

  if [ -d "${contriblib}" ]; then
    for a in "${contriblib}"/*.a; do
      [ -e "$a" ] && cp -a "$a" "${sdir}/"
    done
  fi

  if [ -f "${out}/libvlccore.a" ]; then
    "${AR}" d "${out}/libvlccore.a" revision.o 2>/dev/null || true
    "${RANLIB:-ranlib}" "${out}/libvlccore.a" 2>/dev/null || true
  fi

  local uniq
  uniq="$(printf '%s\n' ${syms} | grep -E '^vlc_entry__' | sort -u)"

  local gen="${sdir}/vlc-static-plugins.c"
  {
    echo '/* Auto-generated: static libvlc plugin registration table. */'
    echo 'typedef int (*vlc_set_cb)(void *, void *, int, ...);'
    printf '%s\n' "${uniq}" | while read -r s; do
      [ -n "$s" ] && echo "extern int ${s}(vlc_set_cb, void *);"
    done
    echo 'typedef int (*vlc_plugin_cb)(vlc_set_cb, void *);'
    echo '__attribute__((visibility("default")))'
    echo 'const vlc_plugin_cb vlc_static_modules[] = {'
    printf '%s\n' "${uniq}" | while read -r s; do
      [ -n "$s" ] && echo "    ${s},"
    done
    echo '    (vlc_plugin_cb)0'
    echo '};'
  } > "${gen}"

  printf '%s\n' "${staged_modules}" | sed '/^$/d' | sort -u > "${sdir}/static-modules.txt"

  ( cd "${sdir}" \
      && ${CC} ${cflags} -c -o vlc-static-plugins.o vlc-static-plugins.c \
      && "${AR}" rc libvlcstaticmodules.a vlc-static-plugins.o \
      && "${RANLIB:-ranlib}" libvlcstaticmodules.a \
      && rm -f vlc-static-plugins.o )

  local n
  n="$(printf '%s\n' "${uniq}" | grep -c '^vlc_entry__' || true)"
  log "Staged $(ls "${sdir}"/*.a 2>/dev/null | wc -l | tr -d ' ') archives; ${n} static modules registered for ${rid}"
}

# bundle_linux_deps <artifact-dir>
# Makes a Linux artifact relocatable: copies the transitive, non-system shared
# library closure of libvlc + every plugin next to libvlc, then rewrites RPATHs
# to $ORIGIN so the runtime resolves without the build machine's -dev packages.
bundle_linux_deps() {
  local out="$1"
  command -v patchelf >/dev/null 2>&1 || die "patchelf is required for Linux dependency bundling"
  log "Bundling Linux dependency closure for ${out}"

  # Libraries provided by every glibc host / dynamic loader: never bundle these.
  local sys_re='^(ld-linux.*|linux-vdso.*|libc|libm|libdl|libpthread|librt|libresolv|libutil|libnsl|libanl)\.so'

  local copied=1 round=0
  while [ "${copied}" -gt 0 ] && [ "${round}" -lt 12 ]; do
    copied=0
    round=$((round + 1))
    local obj dep name path base
    while IFS= read -r -d '' obj; do
      while IFS= read -r dep; do
        case "${dep}" in
          *"=>"*"/"*) : ;;
          *) continue ;;
        esac
        name="$(printf '%s\n' "${dep}" | awk '{print $1}')"
        path="$(printf '%s\n' "${dep}" | awk '{print $3}')"
        base="$(basename "${name}")"
        printf '%s\n' "${base}" | grep -Eq "${sys_re}" && continue
        [ -e "${out}/${base}" ] && continue
        if [ -f "${path}" ]; then
          cp -aL "${path}" "${out}/${base}"
          copied=$((copied + 1))
        fi
      done < <(ldd "${obj}" 2>/dev/null || true)
    done < <(find "${out}" -type f \( -name '*.so' -o -name '*.so.*' \) -print0)
    log "bundle round ${round}: copied ${copied} new dependency object(s)"
  done

  # $ORIGIN for root libs; plugins are two dirs deep (plugins/<cat>/x.so) so add
  # $ORIGIN/../.. to reach the bundled libraries and libvlccore at the root.
  local f
  while IFS= read -r -d '' f; do
    case "${f}" in
      "${out}"/plugins/*/*) patchelf --set-rpath '$ORIGIN:$ORIGIN/../..' "${f}" 2>/dev/null || true ;;
      *)                    patchelf --set-rpath '$ORIGIN' "${f}" 2>/dev/null || true ;;
    esac
  done < <(find "${out}" -type f \( -name '*.so' -o -name '*.so.*' \) -print0)

  log "Bundled runtime now has $(find "${out}" -maxdepth 1 -name '*.so*' | wc -l | tr -d ' ') root shared objects"
}
