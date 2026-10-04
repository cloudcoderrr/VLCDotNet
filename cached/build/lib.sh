#!/usr/bin/env bash
#
# Shared helpers for building libvlc from VideoLAN source against the FULL
# VLC third-party dependency closure ("contribs"), built from source *inside*
# the GitHub Actions runner images and cached in-repo.
#
# Unlike prebuilt/build/lib.sh (which downloads VideoLAN's published contrib
# bundles) and build/lib.sh (which rebuilds a minimal contrib set every run),
# this pipeline runs in two phases wired through the repository:
#
#   Phase 1 (contrib):  build_full_contrib + pack_contrib
#       Build the full default contrib set for one target with VLC's contrib
#       system, then compress + split (<100 MB parts) the resulting prefix into
#       cached/contribs/vlc-<series>.0/<rid>/ and commit it.
#   Phase 2 (libvlc):   restore_contrib + configure/make
#       A later action checks out the repo, restores the cached contrib prefix,
#       and links libvlc against it. No contrib fetch/build is needed.
#
# Splitting the two phases keeps either action within a single runner's disk
# budget (a full contrib closure plus a libvlc build will not co-reside).
#
set -euo pipefail

# The VLC tag to build. Kept in sync with <VlcVersion> in Directory.Build.props.
VLC_VERSION="${VLC_VERSION:-3.0.23}"
VLC_GIT="${VLC_GIT:-https://code.videolan.org/videolan/vlc.git}"

# Series selector: 3 (stable, default) or 4 (unreleased 4.0 preview). VLC_REF is
# the git ref actually built (a tag for 3.x, a branch/sha for 4.x).
VLC_SERIES="${VLC_SERIES:-3}"
VLC_REF="${VLC_REF:-${VLC_VERSION}}"

BUILD_LIB_DIR="$( cd "$(dirname "${BASH_SOURCE[0]}")" && pwd )"
REPO_ROOT="$( cd "${BUILD_LIB_DIR}/../.." && pwd )"
PATCH_DIR="${PATCH_DIR:-${REPO_ROOT}/cached/patches/vlc-${VLC_SERIES}.0}"
WORK_DIR="${WORK_DIR:-${REPO_ROOT}/cached/build/_work}"
ARTIFACTS_DIR="${ARTIFACTS_DIR:-${REPO_ROOT}/artifacts}"
# In-repo cache of the compressed, split contrib prefixes (Phase 1 output).
CONTRIB_CACHE_DIR="${CONTRIB_CACHE_DIR:-${REPO_ROOT}/cached/contribs/vlc-${VLC_SERIES}.0}"
# Largest git-committed part size. GitHub rejects individual files >100 MiB, so
# stay safely under that.
CONTRIB_PART_SIZE="${CONTRIB_PART_SIZE:-95000000}"
CONTRIB_ZSTD_LEVEL="${CONTRIB_ZSTD_LEVEL:-19}"

# Baseline contrib bootstrap prune applied on every target. Drops package GROUPS
# and individual packages that are NOT used by a libvlc file-playback runtime and
# that otherwise fail / are fragile on the GitHub runners:
#   --disable-disc / --disable-net  disc + network groups (cddb, cdio, dvd*,
#                                   bluray, gnutls, srt, live555, smb2, ...)
#   mpg123                          MP3 is decoded by ffmpeg (autoreconf/link fails)
#   opencv4/opencv/protobuf         video analysis (pulls protobuf/protoc)
#   chromaprint                     audio fingerprinting (libvlc --disable-chromaprint)
#   libplacebo/projectM/goom        GPU render + visualizations (need vulkan/GL/FFTW)
#   qt/qtdeclarative/qtsvg/...      Qt GUI (libvlc --disable-qt)
#   medialibrary                    media database (needs sqlite; not used by tests)
#   breakpad                        crash reporting
#   xcb + X11                       X11 video output (libvlc is headless --disable-xcb)
#   x264/x265/x262                  H.26x ENCODERS (tests transcode to mp4v/mp4a via
#                                   ffmpeg; decode is ffmpeg/VideoToolbox/MediaCodec)
#   lua                             VLC scripting (not a required test module; fails
#                                   the cross builds on the arm64 macOS runner)
#   luac                            lua's host compiler tool (cross-build only runs
#                                   on native; PKGS_TOOLS, not covered by disable-lua)
#   taglib                          metadata reader (not a required test module)
#   gpg-error/gcrypt                crypto (unconditional PKGS; only net packages use
#                                   them; gpg-error's mkheader OOMs on the iOS build)
# The full codec/demux/subtitle/audio closure is kept. Override to change the set.
CONTRIB_DEFAULT_PRUNE="${CONTRIB_DEFAULT_PRUNE:---disable-disc --disable-net --disable-mpg123 --disable-opencv4 --disable-opencv --disable-protobuf --disable-chromaprint --disable-libplacebo --disable-projectM --disable-goom --disable-qt --disable-qtdeclarative --disable-qtshadertools --disable-qtsvg --disable-qtwayland --disable-medialibrary --disable-breakpad --disable-xcb --disable-x264 --disable-x265 --disable-x262 --disable-lua --disable-luac --disable-taglib --disable-gpg-error --disable-gcrypt}"

log()  { printf '\033[1;36m[vlcdotnet-cached]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[vlcdotnet-cached]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[vlcdotnet-cached]\033[0m %s\n' "$*" >&2; exit 1; }

# sha256_of <file>
# Portable sha256 (Linux coreutils sha256sum / macOS shasum).
sha256_of() {
  if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | awk '{print $1}';
  elif command -v shasum >/dev/null 2>&1; then shasum -a 256 "$1" | awk '{print $1}';
  else die "no sha256 tool (sha256sum/shasum) found"; fi
}

# clone_vlc <dest>
# Shallow-clones the pinned VLC ref (tag for 3.x, branch/sha for 4.x).
clone_vlc() {
  local dest="$1"
  if [ ! -d "${dest}/.git" ]; then
    log "Cloning VLC ${VLC_REF} -> ${dest}"
    if ! git clone --depth 1 --branch "${VLC_REF}" "${VLC_GIT}" "${dest}" 2>/dev/null; then
      log "Shallow branch clone failed; full clone then checkout ${VLC_REF}"
      git clone "${VLC_GIT}" "${dest}"
      git -C "${dest}" checkout "${VLC_REF}"
    fi
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

# have_cached_contrib <rid>
# Succeeds when a committed contrib cache exists for <rid>.
have_cached_contrib() {
  [ -f "${CONTRIB_CACHE_DIR}/$1/MANIFEST" ]
}

# build_vlc_tools <vlc-src>
# Builds VLC's pinned bootstrap toolchain under extras/tools (autoconf, automake,
# libtool, pkg-config, nasm, gas-preprocessor, ...). The full contrib set needs
# consistent build tools; the host's autotools otherwise trip package autoreconf
# steps (e.g. mpg123's `undefined macro: LT_SYS_MODULE_EXT`). The contrib
# main.mak automatically prepends extras/tools/build/bin to PATH, so building
# these here is all that is required. Best-effort: on partial failure we fall
# back to host tools and let the affected package surface during the build.
build_vlc_tools() {
  local src="$1"
  [ -d "${src}/extras/tools" ] || { warn "no extras/tools in VLC source; using host tools"; return 0; }
  if [ -x "${src}/extras/tools/build/bin/libtoolize" ] || [ -x "${src}/extras/tools/build/bin/autoconf" ]; then
    log "VLC extras/tools already built"
    return 0
  fi
  log "Building VLC extras/tools (pinned autotools/pkg-config/nasm)"
  (
    cd "${src}/extras/tools"
    ./bootstrap
    make -j"$(jobs)" || make || warn "extras/tools build incomplete; continuing with host tools"
  )
}

# build_full_contrib <vlc-src> <build-subdir> <triplet> [-- <bootstrap-flag>...]
#
# Builds the full VLC contrib set from source into <vlc-src>/contrib/<triplet>.
# A baseline prune (CONTRIB_DEFAULT_PRUNE) drops the disc + network package
# GROUPS and mpg123, none of which are used for local file playback and all of
# which fail to build cleanly on the GitHub runners (cddb AM_ICONV, mpg123
# LT_SYS_MODULE_EXT / link). Everything else VLC enables for the host triplet is
# built. Reads an optional CONTRIB_ENV array for platform selectors
# (HAVE_ANDROID=1, BUILDFORIOS=1, HAVE_MACOSX=1, ...) and an optional CONTRIB_MK
# array for extra make variables. Extra bootstrap flags may be passed after a
# literal -- to prune additional packages that cannot cross-build for a target.
build_full_contrib() {
  local src="$1" subdir="$2" triplet="$3"; shift 3
  local -a bootstrap_flags=()
  if [ "${1:-}" = "--" ]; then shift; bootstrap_flags=("$@"); fi
  # Baseline prune on every target (prepended so callers' prunes still apply).
  local -a default_prune=(${CONTRIB_DEFAULT_PRUNE})
  bootstrap_flags=(${default_prune[@]+"${default_prune[@]}"} ${bootstrap_flags[@]+"${bootstrap_flags[@]}"})
  # macOS runners ship bash 3.2, where expanding an empty array under set -u
  # errors; guard assignments by length and expand with the ${a[@]+"${a[@]}"} idiom.
  local -a cenv=() cmk=()
  if declare -p CONTRIB_ENV >/dev/null 2>&1 && [ "${#CONTRIB_ENV[@]}" -gt 0 ]; then cenv=("${CONTRIB_ENV[@]}"); fi
  if declare -p CONTRIB_MK  >/dev/null 2>&1 && [ "${#CONTRIB_MK[@]}"  -gt 0 ]; then cmk=("${CONTRIB_MK[@]}"); fi

  # Build the pinned host toolchain first so every package autoreconfs cleanly.
  build_vlc_tools "${src}"

  local cbuild="${src}/contrib/${subdir}"
  mkdir -p "${cbuild}"
  log "Bootstrapping full contrib set for ${triplet}"
  (
    cd "${cbuild}"
    env ${cenv[@]+"${cenv[@]}"} ../bootstrap --host="${triplet}" ${bootstrap_flags[@]+"${bootstrap_flags[@]}"}
  )
  log "Fetching contrib sources for ${triplet}"
  ( cd "${cbuild}" && env ${cenv[@]+"${cenv[@]}"} make ${cmk[@]+"${cmk[@]}"} -j"$(jobs)" fetch )
  log "Building full contrib set for ${triplet} (long pole)"
  ( cd "${cbuild}" && { env ${cenv[@]+"${cenv[@]}"} make ${cmk[@]+"${cmk[@]}"} -j"$(jobs)" \
      || env ${cenv[@]+"${cenv[@]}"} make ${cmk[@]+"${cmk[@]}"} -j1 ; } )
  [ -d "${src}/contrib/${triplet}/lib" ] || [ -d "${src}/contrib/${triplet}/lib64" ] \
    || die "Contrib build produced no lib dir at ${src}/contrib/${triplet}"
  log "Full contrib set installed at ${src}/contrib/${triplet}"
}

# pack_contrib <vlc-src> <triplet> <rid>
# Compresses <vlc-src>/contrib/<triplet> and splits it into <100 MB parts under
# cached/contribs/vlc-<series>.0/<rid>/ with a MANIFEST (sha + origin prefix) so
# it can be committed to the repository and restored by a later action.
pack_contrib() {
  local src="$1" triplet="$2" rid="$3"
  local prefix="${src}/contrib/${triplet}"
  [ -d "${prefix}" ] || die "No contrib prefix to pack at ${prefix}"
  command -v zstd >/dev/null 2>&1 || die "zstd is required to pack contribs"

  local out="${CONTRIB_CACHE_DIR}/${rid}"
  rm -rf "${out}"; mkdir -p "${out}"
  local tmp; tmp="$(mktemp -d)"
  local tarball="${tmp}/contrib.tar.zst"

  log "Packing contrib prefix for ${rid} (zstd -${CONTRIB_ZSTD_LEVEL})"
  # Store the prefix relative to contrib/ so it restores as contrib/<triplet>.
  tar -C "${src}/contrib" -cf - "${triplet}" | zstd -"${CONTRIB_ZSTD_LEVEL}" -T0 -q -f -o "${tarball}"

  local sha bytes
  sha="$(sha256_of "${tarball}")"
  bytes="$(wc -c < "${tarball}" | tr -d ' ')"

  # Alphabetic suffixes keep GNU (Linux) and BSD (macOS) split interoperable.
  ( cd "${out}" && split -b "${CONTRIB_PART_SIZE}" -a 3 "${tarball}" "contrib.tar.zst." )
  local nparts; nparts="$(ls "${out}"/contrib.tar.zst.* 2>/dev/null | wc -l | tr -d ' ')"

  {
    echo "rid=${rid}"
    echo "triplet=${triplet}"
    echo "series=${VLC_SERIES}"
    echo "vlc_version=${VLC_VERSION}"
    echo "vlc_ref=${VLC_REF}"
    echo "build_prefix=${prefix}"
    echo "archive=contrib.tar.zst"
    echo "parts=${nparts}"
    echo "part_size=${CONTRIB_PART_SIZE}"
    echo "sha256=${sha}"
    echo "bytes=${bytes}"
    echo "created=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    echo "restore=cat contrib.tar.zst.* | zstd -d | tar -x"
  } > "${out}/MANIFEST"

  rm -rf "${tmp}"
  log "Cached ${rid} contrib: ${nparts} part(s), $(( bytes / 1024 / 1024 )) MiB compressed"
  ( cd "${out}" && ls -l )
}

# restore_contrib <vlc-src> <triplet> <rid>
# Reassembles and extracts the committed contrib cache for <rid> into
# <vlc-src>/contrib/<triplet>, verifies its checksum, and rewrites the recorded
# build prefix to the current location so pkg-config/libtool files resolve.
restore_contrib() {
  local src="$1" triplet="$2" rid="$3"
  local cache="${CONTRIB_CACHE_DIR}/${rid}"
  [ -f "${cache}/MANIFEST" ] || die "No cached contrib for ${rid} at ${cache} (run the contribs-* workflow first)"
  command -v zstd >/dev/null 2>&1 || die "zstd is required to restore contribs"

  local build_prefix sha
  build_prefix="$(sed -n 's/^build_prefix=//p' "${cache}/MANIFEST")"
  sha="$(sed -n 's/^sha256=//p' "${cache}/MANIFEST")"

  local tmp; tmp="$(mktemp -d)"
  local tarball="${tmp}/contrib.tar.zst"
  log "Restoring cached contrib for ${rid} from ${cache}"
  cat "${cache}"/contrib.tar.zst.* > "${tarball}"

  if [ -n "${sha}" ]; then
    local got; got="$(sha256_of "${tarball}")"
    [ "${got}" = "${sha}" ] || die "Contrib checksum mismatch for ${rid} (expected ${sha}, got ${got})"
  fi

  mkdir -p "${src}/contrib"
  rm -rf "${src}/contrib/${triplet}"
  zstd -dc "${tarball}" | tar -C "${src}/contrib" -xf -
  rm -rf "${tmp}"

  local new_prefix="${src}/contrib/${triplet}"
  [ -d "${new_prefix}" ] || die "Restore did not produce ${new_prefix}"

  if [ -n "${build_prefix}" ] && [ "${build_prefix}" != "${new_prefix}" ]; then
    log "Rewriting contrib prefix ${build_prefix} -> ${new_prefix}"
    # Only text metadata embeds the absolute prefix: *.pc, *.la, *-config.
    # Pass both paths through %ENV so their '/' never lands in the perl s///
    # source as a delimiter, and \Q-quote the pattern. Exported so find -exec's
    # child perl inherits them.
    export CONTRIB_OLD_PREFIX="${build_prefix}" CONTRIB_NEW_PREFIX="${new_prefix}"
    find "${new_prefix}" -type f \( -name '*.pc' -o -name '*.la' -o -name '*-config' \) \
      -exec perl -pi -e 's/\Q$ENV{CONTRIB_OLD_PREFIX}\E/$ENV{CONTRIB_NEW_PREFIX}/g' {} +
    unset CONTRIB_OLD_PREFIX CONTRIB_NEW_PREFIX
  fi
  log "Restored contrib prefix at ${new_prefix}"
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
    # libtool names the object libvlccore_la-revision.o (not plain revision.o),
    # so look up the real member name rather than assuming a fixed one; drop
    # libvlccore's copy of psz_vlc_changeset so the app links exactly one.
    local revobj
    revobj="$("${AR}" t "${out}/libvlccore.a" 2>/dev/null | grep -E '(^|[-_])revision\.o$' | head -n1)"
    if [ -n "${revobj}" ]; then
      "${AR}" d "${out}/libvlccore.a" "${revobj}" 2>/dev/null || true
      "${RANLIB:-ranlib}" "${out}/libvlccore.a" 2>/dev/null || true
    fi
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
