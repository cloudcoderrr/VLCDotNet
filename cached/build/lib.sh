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

# Baseline contrib bootstrap prune applied on every target. The cached pipeline
# ships VLC's COMPLETE native transport/network stack (see
# cached/FEATURE-RESTORATION-TASK.md): the network package GROUP is KEPT so
# gnutls (TLS), live555 (RTSP), librist (RIST), smb2/libdsm (SMB), microdns
# (mDNS/Bonjour discovery), nfs, upnp, srt and shout/ssh2 all build, and the
# contrib ffmpeg picks up its network protocols (AVIO). Only host-owned or
# genuinely-unbuildable NON-transport packages are dropped here:
#   opencv4/opencv/protobuf         computer-vision analysis (specialist extra)
#   chromaprint                     audio fingerprinting (specialist extra)
#   libplacebo                      GPU HDR renderer (needs glslang/shaderc/vulkan;
#                                   HDR/tone-map handled by other paths for now)
#   projectM/goom                   external visualization engines (specialist)
#   qt/qtdeclarative/qtsvg/...      Qt GUI (libvlc --disable-qt)
#   medialibrary                    media database (host-owned library/indexing)
#   breakpad                        crash reporting (host-owned)
#   xcb                             CONTRIB xcb libs; Linux links the SYSTEM
#                                   libxcb/-dev instead (deps-linux.sh)
#   x262                            MPEG-2 ENCODER backend (not a required module;
#                                   not shipped by the official win-x64 archive)
#   aom                             AV1 ENCODER (AV1 DECODE is covered by dav1d;
#                                   libaom NEON fails to build on armv7)
#   lua / luac                      VLC scripting (host-owned; excluded)
#   cddb                            CD metadata lookup (needs BOTH disc AND net;
#                                   re-enabled by the net restoration but not a
#                                   restoration feature, and its configure trips
#                                   -Werror=unguarded-availability-new on the
#                                   macOS SDK). Never shipped, so keep it pruned.
# gnutls' crypto deps (nettle/gmp/gcrypt/gpg-error) now build for the TLS stack;
# the Apple targets re-prune them in APPLE_PRUNE and use securetransport instead
# (gpg-error's mkheader OOMs under the iOS cross build). Override
# CONTRIB_DEFAULT_PRUNE to change the set.
CONTRIB_DEFAULT_PRUNE="${CONTRIB_DEFAULT_PRUNE:---disable-opencv4 --disable-opencv --disable-protobuf --disable-chromaprint --disable-libplacebo --disable-projectM --disable-goom --disable-qt --disable-qtdeclarative --disable-qtshadertools --disable-qtsvg --disable-qtwayland --disable-medialibrary --disable-breakpad --disable-xcb --disable-x262 --disable-aom --disable-lua --disable-luac --disable-cddb}"

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
    # extras/tools are HOST executables (meson, ninja, cmake, nasm, pkg-config all
    # run on the build machine). Cross targets (Apple/Android) export the TARGET
    # CC/CXX/CFLAGS globally for the contrib set; inheriting them here cross-builds
    # the tools for the target, which then cannot run on the host (e.g. VLC 4.0's
    # cmake zlib probes `ninja --version` and the iOS ninja is "Subprocess killed").
    # When the caller provides a build-machine toolchain (*_FOR_BUILD), use it.
    if [ -n "${CC_FOR_BUILD:-}" ]; then
      export CC="${CC_FOR_BUILD}" CXX="${CXX_FOR_BUILD:-${CC_FOR_BUILD}}"
      export CFLAGS="${CFLAGS_FOR_BUILD:-}" CXXFLAGS="${CXXFLAGS_FOR_BUILD:-}"
      export LDFLAGS="${LDFLAGS_FOR_BUILD:-}" CPPFLAGS="${CPPFLAGS_FOR_BUILD:-}"
      unset OBJCFLAGS OBJC
    fi
    ./bootstrap
    make -j"$(jobs)" || make || warn "extras/tools build incomplete; continuing with host tools"
  )
}

# build_full_contrib <vlc-src> <build-subdir> <triplet> [-- <bootstrap-flag>...]
#
# Builds the full VLC contrib set from source into <vlc-src>/contrib/<triplet>.
# A baseline prune (CONTRIB_DEFAULT_PRUNE) drops only host-owned / non-transport
# specialist packages (computer-vision, Qt, visualizations, encoders of last
# resort); the network package GROUP is KEPT so the full native transport stack
# (TLS/RTSP/RIST/SMB/mDNS/...) and the network-enabled contrib ffmpeg build.
# Everything else VLC enables for the host triplet is
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

# ---------------------------------------------------------------------------
# Split contrib build (feature-parity closure is too big/slow for one runner).
#
# The full parity closure (ffmpeg + x264/x265 + dav1d + the whole codec/demux/
# subtitle/disc/metadata set) exceeds a single runner's time/disk budget on the
# slower targets, so Phase 1 is split into ORDERED GROUPS, one CI job each:
#
#   group "heavy":  make .ffmpeg .x265 .x264   (the long poles, isolated)
#   group "rest":   bare make                  (mops up every other enabled pkg)
#
# A later group RESTORES the accumulated prefix + stamp files + tarball cache the
# earlier group produced (VLC contrib skips any package whose .<pkg> stamp is
# already present), so no single job builds the whole closure and nothing is
# rebuilt. All group jobs for one RID run on the same runner OS at identical
# absolute paths within one workflow run, so the state restores verbatim with no
# prefix rewrite. The final group packs the committed cache (pack_contrib).
# ---------------------------------------------------------------------------

# Ordered group names. Override CONTRIB_GROUPS to re-split (e.g. "ffmpeg x265 rest").
CONTRIB_GROUPS="${CONTRIB_GROUPS:-heavy rest}"
# Scratch dir for the resumable inter-group state archive (uploaded/downloaded as
# a workflow artifact between the chained group jobs).
CONTRIB_STATE_DIR="${CONTRIB_STATE_DIR:-${REPO_ROOT}/cached/build/_state}"

# contrib_group_targets <group> -> make targets for the group ("" = bare make).
contrib_group_targets() {
  case "$1" in
    heavy) echo ".ffmpeg .x265 .x264" ;;
    rest)  echo "" ;;
    # Finer splits callers may opt into via CONTRIB_GROUPS.
    ffmpeg) echo ".ffmpeg" ;;
    x265)  echo ".x265" ;;
    x264)  echo ".x264 .x26410b" ;;
    *) die "unknown contrib group: $1 (set CONTRIB_GROUPS / contrib_group_targets)" ;;
  esac
}

contrib_first_group() { set -- ${CONTRIB_GROUPS}; echo "$1"; }
contrib_last_group()  { set -- ${CONTRIB_GROUPS}; eval echo "\${$#}"; }

# contrib_reclaim <contrib-build-subdir>
# After a group installs its packages into the prefix, delete the extracted
# source/build trees (the disk hogs) while keeping the .<pkg> stamp files,
# config.mak and Makefile so a later group resumes without rebuilding.
contrib_reclaim() {
  local cbuild="$1"
  [ -d "${cbuild}" ] || return 0
  find "${cbuild}" -mindepth 1 -maxdepth 1 -type d -exec rm -rf {} + 2>/dev/null || true
  log "Reclaimed contrib build trees under ${cbuild}"
}

# save_contrib_state <vlc-src> <subdir> <triplet> <rid>
# Packs the resumable state (install prefix + build-subdir stamps/config +
# tarball cache, all relative to contrib/) for the next group job.
save_contrib_state() {
  local src="$1" subdir="$2" triplet="$3" rid="$4"
  command -v zstd >/dev/null 2>&1 || die "zstd is required to save contrib state"
  local out="${CONTRIB_STATE_DIR}/${rid}"
  rm -rf "${out}"; mkdir -p "${out}"
  local -a members=("${triplet}" "${subdir}")
  [ -d "${src}/contrib/tarballs" ] && members+=("tarballs")
  ( cd "${src}/contrib" && tar -cf - "${members[@]}" ) \
    | zstd -3 -T0 -q -f -o "${out}/state.tar.zst"
  log "Saved contrib state for ${rid}: $(du -h "${out}/state.tar.zst" 2>/dev/null | cut -f1)"
}

# load_contrib_state <vlc-src> <rid>
load_contrib_state() {
  local src="$1" rid="$2"
  local in="${CONTRIB_STATE_DIR}/${rid}/state.tar.zst"
  [ -f "${in}" ] || die "No contrib state to resume for ${rid} at ${in} (run the earlier group first)"
  command -v zstd >/dev/null 2>&1 || die "zstd is required to load contrib state"
  mkdir -p "${src}/contrib"
  zstd -dc "${in}" | tar -C "${src}/contrib" -xf -
  log "Restored contrib state for ${rid} from ${in}"
}

# build_contrib_group <vlc-src> <subdir> <triplet> <rid> <group> [-- <bootstrap-flag>...]
# Builds ONE contrib group. The first group bootstraps + fetches; later groups
# restore the previous group's state. The last group packs the committed cache.
build_contrib_group() {
  local src="$1" subdir="$2" triplet="$3" rid="$4" group="$5"; shift 5
  local -a bootstrap_flags=()
  if [ "${1:-}" = "--" ]; then shift; bootstrap_flags=("$@"); fi
  local -a default_prune=(${CONTRIB_DEFAULT_PRUNE})
  bootstrap_flags=(${default_prune[@]+"${default_prune[@]}"} ${bootstrap_flags[@]+"${bootstrap_flags[@]}"})
  local -a cenv=() cmk=()
  if declare -p CONTRIB_ENV >/dev/null 2>&1 && [ "${#CONTRIB_ENV[@]}" -gt 0 ]; then cenv=("${CONTRIB_ENV[@]}"); fi
  if declare -p CONTRIB_MK  >/dev/null 2>&1 && [ "${#CONTRIB_MK[@]}"  -gt 0 ]; then cmk=("${CONTRIB_MK[@]}"); fi

  build_vlc_tools "${src}"
  local cbuild="${src}/contrib/${subdir}"

  if [ "${group}" = "$(contrib_first_group)" ]; then
    mkdir -p "${cbuild}"
    log "[${group}] Bootstrapping contrib for ${triplet}"
    ( cd "${cbuild}" && env ${cenv[@]+"${cenv[@]}"} ../bootstrap --host="${triplet}" ${bootstrap_flags[@]+"${bootstrap_flags[@]}"} )
    log "[${group}] Fetching all contrib sources for ${triplet}"
    ( cd "${cbuild}" && env ${cenv[@]+"${cenv[@]}"} make ${cmk[@]+"${cmk[@]}"} -j"$(jobs)" fetch )
  else
    load_contrib_state "${src}" "${rid}"
    # Keep make from treating restored stamps as stale against their prereqs.
    find "${cbuild}" -maxdepth 1 -name '.*' -type f -exec touch {} + 2>/dev/null || true
  fi

  local targets; targets="$(contrib_group_targets "${group}")"
  # Drop any explicitly-named group target that the bootstrap prune disables, so
  # the heavy group does not force-build a package a caller pruned (e.g. x264/x265
  # on Mac Catalyst, whose asm objects are tagged 'macOS' not 'macabi' and then
  # fail to link into a Catalyst consumer app).
  if [ -n "${targets}" ]; then
    local _ft="" _t _pkg
    for _t in ${targets}; do
      _pkg="${_t#.}"
      case " ${bootstrap_flags[*]} " in
        *" --disable-${_pkg} "*) log "[${group}] skipping pruned target ${_t}" ;;
        *) _ft="${_ft} ${_t}" ;;
      esac
    done
    targets="${_ft# }"
  fi
  log "[${group}] Building contrib targets: ${targets:-<all remaining enabled>}"
  ( cd "${cbuild}" && { env ${cenv[@]+"${cenv[@]}"} make ${cmk[@]+"${cmk[@]}"} -j"$(jobs)" ${targets} \
      || env ${cenv[@]+"${cenv[@]}"} make ${cmk[@]+"${cmk[@]}"} -j1 ${targets} ; } )

  contrib_reclaim "${cbuild}"

  if [ "${group}" = "$(contrib_last_group)" ]; then
    [ -d "${src}/contrib/${triplet}/lib" ] || [ -d "${src}/contrib/${triplet}/lib64" ] \
      || die "Contrib build produced no lib dir at ${src}/contrib/${triplet}"
    log "[${group}] Final group complete; packing committed cache for ${rid}"
    pack_contrib "${src}" "${triplet}" "${rid}"
  else
    save_contrib_state "${src}" "${subdir}" "${triplet}" "${rid}"
  fi
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

# export_gnutls_static_libs <contrib-prefix>
# Appends the contrib gnutls crypto closure (hogweed/nettle/gmp) to LIBS so it
# lands at the END of EVERY plugin link line (after each module's own -lgnutls).
# VLC 3.0's contrib gnutls statically bundles a nettle wrapper whose objects
# reference base nettle/hogweed/gmp symbols (nettle_aes*, nettle_cmac_*,
# nettle_umac*, nettle_hmac_*, nettle_mpz_*, ...). EVERY plugin that pulls gnutls
# -- the gnutls TLS module AND transitive consumers like vnc (libvncclient), srt
# and rist -- links with non-static pkg-config, so the private nettle closure is
# missing and the link fails with "undefined reference to nettle_*". Putting the
# closure in LIBS (appended last by automake) fixes all of them at once; the
# archives are wrapped in --start-group as absolute paths so neither libtool nor
# the single-pass linker can reorder them, and unreferenced plugins pull nothing.
# GNUTLS_STATIC_CLOSURE is exported too so scripts that build their own configure
# LIBS (Android) can fold it in. No-op when gnutls is not in the contrib (Apple
# uses securetransport).
export_gnutls_static_libs() {
  local prefix="$1"
  local libdir="${prefix}/lib"
  [ -f "${libdir}/libnettle.a" ] || return 0
  local members="" a
  for a in libhogweed.a libnettle.a libgmp.a; do
    [ -f "${libdir}/${a}" ] && members="${members}${members:+,}${libdir}/${a}"
  done
  [ -n "${members}" ] || return 0
  # gnutls also pulls zlib (compress/uncompress/compressBound) on some targets
  # (e.g. the Android srt plugin links gnutls); -lz resolves it from the system /
  # NDK / mingw zlib and is harmless where unneeded.
  local closure="-Wl,--start-group,${members},--end-group -lz"
  export GNUTLS_STATIC_CLOSURE="${closure}"
  export LIBS="${LIBS:-}${LIBS:+ }${closure}"
  log "Appended gnutls crypto closure to LIBS: ${closure}"
}

# strip_excluded_plugins <artifact-dir>
# Optional denylist filter for the staged dynamic runtime. The cached pipeline
# now SHIPS VLC's complete native transport/network stack (HTTP/HTTPS/FTP/RTP/
# RTSP/UDP/TCP/SAT-IP/RIST/SMB/mDNS/discovery/...), so the denylist is EMPTY by
# default and nothing is stripped. It stays as a hook: set VLC_EXCLUDED_NET_MODULES
# (space-separated module base names, no lib/_plugin affixes) in the environment
# to drop specific modules from both the dynamic file tree and the Apple static
# registration table (stage_static_vlc consumes the same list).
VLC_EXCLUDED_NET_MODULES="${VLC_EXCLUDED_NET_MODULES-}"

strip_excluded_plugins() {
  local out="$1"
  [ -d "${out}/plugins" ] || return 0
  local n removed=0 f
  for n in ${VLC_EXCLUDED_NET_MODULES}; do
    while IFS= read -r -d '' f; do
      rm -f "$f" && removed=$((removed + 1))
    done < <(find "${out}/plugins" -type f -name "lib${n}_plugin.*" -print0 2>/dev/null)
  done
  log "Stripped ${removed} excluded native-networking plugin file(s) from ${out}"
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

  # Apply the optional module denylist (empty by default: ship the full stack).
  strip_excluded_plugins "${out}"

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
      local base modname
      base="$(basename "$a" .a)"
      # Register every plugin archive so the full native transport stack (http/
      # https/ftp/rtp/rtsp/udp/tcp/sat-ip/rist/smb2/sap/podcast/securetransport/
      # ...) is force-loaded into the static framework. VLC_EXCLUDED_NET_MODULES
      # (empty by default) may name modules to skip via the environment.
      modname="$(printf '%s' "${base#lib}" | sed 's/_plugin$//')"
      case " ${VLC_EXCLUDED_NET_MODULES} " in
        *" ${modname} "*) continue ;;
      esac
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

# stage_dynamic_vlc <rid> <install-prefix> <platform> <min-ver-flag> <arch> <sdkroot> <extra-cflags>
# Apple dynamic-linkage delivery (iOS / simulator / Mac Catalyst). Links the
# build-internal static libvlc.a + libvlccore.a + every staged plugin archive +
# the generated vlc_static_modules[] table into ONE dynamic Mach-O inside
# VLCDotNet.framework/, so the .NET consumer links libvlc *dynamically* (no
# app-linked archives, no libvlc __Internal) as required by the parity scope.
# Plugins remain statically linked *inside* this dynamic framework (build-
# internal archives are allowed) and register at libvlc_new() because the table
# is force-loaded. Must run after stage_static_vlc (reuses its <out>/static/).
stage_dynamic_vlc() {
  local rid="$1" prefix="$2" platform="$3" minver="$4" arch="$5" sdkroot="$6" extracf="$7"
  local out="${ARTIFACTS_DIR}/${rid}"
  local sdir="${out}/static"
  local fw="${out}/VLCDotNet.framework"
  [ -f "${out}/libvlc.a" ] && [ -f "${out}/libvlccore.a" ] \
    || { warn "stage_dynamic_vlc: missing libvlc.a/libvlccore.a for ${rid}"; return 1; }
  [ -f "${sdir}/libvlcstaticmodules.a" ] \
    || { warn "stage_dynamic_vlc: missing static module table for ${rid}"; return 1; }

  log "Linking dynamic VLCDotNet.framework for ${rid}"

  # Explicit per-platform framework + system-library closure for VLC's static
  # plugins. pkg-config's libvlc Libs.private is contaminated with macOS-only
  # AppKit and -undefined dynamic_lookup (both invalid on iOS/simulator), so the
  # authoritative set is enumerated here, matching the proven NativeReference
  # Frameworks lists the static consumer targets used. (AudioUnit is NOT a
  # standalone framework on iOS/simulator/macabi — those APIs live in
  # AudioToolbox — so it must not be named here.) Metal / AVKit / CoreImage are
  # required by VLC 4.0's newer Apple plugins (cvpx_gl -> Metal,
  # pictureinpicturecontroller -> AVKit, samplebufferdisplay -> CoreImage) and
  # are harmless on VLC 3. Fully resolved link => self-contained dylib.
  local sysflags=(
    -framework Foundation -framework CoreFoundation -framework CFNetwork
    -framework CoreGraphics -framework CoreText -framework QuartzCore
    -framework CoreMedia -framework CoreVideo -framework CoreAudio
    -framework AudioToolbox -framework AVFoundation -framework AVKit
    -framework VideoToolbox -framework CoreImage -framework Metal
    -framework Security -framework SystemConfiguration
    -lc++ -liconv -lbz2 -lz
  )
  case "${platform}" in
    ios|iossimulator) sysflags+=(-framework UIKit -framework OpenGLES) ;;
    maccatalyst)      sysflags+=(-framework UIKit -framework AppKit) ;;
  esac

  local target
  case "${platform}" in
    ios)          target="${arch}-apple-ios" ;;
    iossimulator) target="${arch}-apple-ios-simulator" ;;
    maccatalyst)  target="${arch}-apple-ios-macabi" ;;
    *)            warn "stage_dynamic_vlc: unexpected platform ${platform}"; return 1 ;;
  esac

  # Match the proven static-consumer linkage (the NativeReference ForceLoad
  # layout): force-load libvlc + libvlccore + the registration table so the full
  # libvlc API is exported and every registered module's vlc_entry__ is retained,
  # and NORMAL-link the plugin + contrib archives so the linker pulls only the
  # objects actually referenced. Force-loading the plugin archives instead pulls
  # their shared helper objects repeatedly (-> "ld: NNN duplicate symbols").
  local archives=() a
  for a in "${sdir}"/*.a; do
    [ -e "$a" ] || continue
    case "$(basename "$a")" in
      libvlcstaticmodules.a) ;;   # force-loaded below
      *)                     archives+=("$a") ;;   # plugins + contribs: normal link
    esac
  done

  mkdir -p "${fw}"
  # shellcheck disable=SC2086
  ${CC} -dynamiclib \
    ${minver} -arch "${arch}" -isysroot "${sdkroot}" -target "${target}" ${extracf} \
    -install_name "@rpath/VLCDotNet.framework/VLCDotNet" \
    -Wl,-force_load,"${out}/libvlc.a" \
    -Wl,-force_load,"${out}/libvlccore.a" \
    -Wl,-force_load,"${sdir}/libvlcstaticmodules.a" \
    "${archives[@]}" \
    "${sysflags[@]}" \
    -o "${fw}/VLCDotNet" \
    || { warn "stage_dynamic_vlc: dynamic framework link failed for ${rid}"; return 1; }

  # Flat framework layout (iOS/Catalyst use a flat bundle, not Versions/).
  cat > "${fw}/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleIdentifier</key><string>net.vlcdotnet.libvlc</string>
  <key>CFBundleName</key><string>VLCDotNet</string>
  <key>CFBundleExecutable</key><string>VLCDotNet</string>
  <key>CFBundlePackageType</key><string>FMWK</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>MinimumOSVersion</key><string>12.0</string>
  <key>CFBundleSupportedPlatforms</key><array><string>${platform}</string></array>
</dict></plist>
PLIST

  log "Built ${fw}/VLCDotNet -> $(lipo -info "${fw}/VLCDotNet" 2>/dev/null || file "${fw}/VLCDotNet" 2>/dev/null)"
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
