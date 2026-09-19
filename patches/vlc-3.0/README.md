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

## How CI applies them

The build workflows clone VLC at the tag in `VlcVersion`
(`Directory.Build.props`) and then run:

```sh
for p in patches/vlc-3.0/*.patch; do
  [ -e "$p" ] || continue
  git -C "$VLC_SRC" apply --3way --verbose "$OLDPWD/$p"
done
```

`--3way` lets a patch still apply (with conflict markers surfaced in the log)
if the surrounding VLC source shifted between releases, which makes version
bumps easier to triage.

## Generating a patch

```sh
cd vlc-src
# make your edits ...
git diff > ../patches/vlc-3.0/0002-my-change.patch
```
