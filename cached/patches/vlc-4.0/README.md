# VLC 4.0 (preview) prebuilt-deps build patches

Unified-diff patches applied (in lexical order) to the VideoLAN VLC checkout
before the **prebuilt-contrib** `VLCDotNet4` build, by `apply_patches` in
[`../../build/lib.sh`](../../build/lib.sh).

Separate from the from-source series ([`../../../patches/vlc-4.0`](../../../patches/vlc-4.0))
so the prebuilt pipeline can carry its own fixes. Starts empty; patches are
added only as CI surfaces a concrete break building libvlc 4.0 (`master`)
against prebuilt dependencies.
