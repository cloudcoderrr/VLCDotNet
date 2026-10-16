#!/usr/bin/env bash
#
# Release preflight: verify the module removal policy held for a tree of staged
# native artifacts. For every RID directory under <artifacts-root> this checks
# that no excluded module (see effective_excluded_modules in build/lib.sh) is
# present in the delivered dynamic plugin tree, and that no excluded module is
# registered in an Apple static framework's static-modules.txt (a stale
# framework built before the policy landed must be rebuilt).
#
# Usage: check-module-policy.sh <artifacts-root>
#
# The current VLC_SERIES selects the series-specific policy (e.g. rav1e on VLC 4).
#
set -euo pipefail
REPO_ROOT="$( cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P )"
# shellcheck source=build/lib.sh
source "${REPO_ROOT}/build/lib.sh"

root="${1:?usage: check-module-policy.sh <artifacts-root>}"
[ -d "${root}" ] || { printf 'check-module-policy: artifacts root not found: %s\n' "${root}" >&2; exit 1; }

# RID artifact dirs are immediate children carrying a dynamic plugin tree and/or
# an Apple static framework manifest.
shopt -s nullglob
rids=()
for d in "${root}"/*/; do
  d="${d%/}"
  if [ -d "${d}/plugins" ] || [ -f "${d}/static/static-modules.txt" ]; then
    rids+=("${d}")
  fi
done
shopt -u nullglob
[ "${#rids[@]}" -gt 0 ] || { printf 'check-module-policy: no RID artifact directories under %s\n' "${root}" >&2; exit 1; }

fail=0
for d in "${rids[@]}"; do
  rid="$(basename "${d}")"

  # Dynamic plugin tree: no excluded plugin file may ship.
  if [ -d "${d}/plugins" ]; then
    while IFS= read -r -d '' f; do
      base="$(basename "$f")"; base="${base#lib}"
      mod="${base%%_plugin.*}"
      if vlc_module_excluded "${mod}"; then
        printf 'check-module-policy: [%s] excluded module shipped: %s (%s)\n' "${rid}" "${mod}" "${f}" >&2
        fail=1
      fi
    done < <(find "${d}/plugins" -type f -name 'lib*_plugin.*' -print0 2>/dev/null)
  fi

  # Apple static registration table: no excluded module may be registered.
  if [ -f "${d}/static/static-modules.txt" ]; then
    while IFS= read -r line; do
      mod="$(printf '%s' "${line}" | tr -d '[:space:]')"
      [ -n "${mod}" ] || continue
      if vlc_module_excluded "${mod}"; then
        printf 'check-module-policy: [%s] excluded module "%s" registered in the Apple static framework; rebuild the framework from current sources\n' "${rid}" "${mod}" >&2
        fail=1
      fi
    done < "${d}/static/static-modules.txt"
  fi
done

[ "${fail}" -eq 0 ] || exit 1
printf 'check-module-policy: policy held for %d RID artifact(s) under %s\n' "${#rids[@]}" "${root}"
