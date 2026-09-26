# VLC 3.0.x source patches

Unified-diff patches in this folder are applied on top of the pristine VLC
`3.0.x` source tree **before** it is configured and built in CI.

## Rules

- One concern per patch file. Name files with a numeric prefix so the apply
  order is deterministic, e.g. `0001-fix-xyz.patch`, `0002-....patch`.
- Patches must be in **unified diff** format with paths relative to the root of
  the VLC source tree (i.e. `a/src/...`, `b/src/...`), so they can be applied
  with `git apply -p1` or `patch -p1`.
- Keep patches minimal and well documented in the patch header (`Subject:` /
  commit message body) so the intent survives a VLC version bump.
- For contrib-only patches, keep hunks limited to packages in the active
  allowlist (`ffmpeg`, `opus`, `ogg`, `ass`, `matroska`) or their direct
  dependencies. Avoid carrying URL rewrites for packages the build no longer
  enables.

## How CI applies them

The build scripts clone VLC at the tag in `VlcVersion`
(`Directory.Build.props`) and then apply each patch in lexical order via
`build/lib.sh`:

```sh
git -C "$VLC_SRC" apply --recount --whitespace=nowarn "$patch" || \
  (cd "$VLC_SRC" && patch -p1 --forward --fuzz=3 --no-backup-if-mismatch < "$patch")
```

`git apply --recount` tolerates small hunk line-count drift without depending on
git index metadata in the patch. The `patch -p1` fallback keeps older, simpler
unified diffs usable when `git apply` is stricter about surrounding context.

## Generating a patch

```sh
cd vlc-src
# make your edits ...
git diff > ../patches/vlc-3.0/0002-my-change.patch
```
