# VLC 4.0 (preview) source-build patches

Unified-diff patches applied (in lexical order) to the VideoLAN VLC checkout
before the **from-source** `VLCDotNet4` build, by `apply_patches` in
[`build/lib.sh`](../../build/lib.sh).

This series tracks the unreleased VLC 4.0 development branch (`master`). It
starts empty: the 3.0 patches under [`../vlc-3.0`](../vlc-3.0) do **not** apply
to the 4.0 tree. Patches are added here only as CI surfaces a concrete build
break on `master`, each with a comment explaining why.

Naming: `NNNN-short-description.patch` (e.g. `0001-fix-foo.patch`). Generate
with `git format-patch` / `git diff` from a VLC checkout and keep each patch
focused on a single fix.
