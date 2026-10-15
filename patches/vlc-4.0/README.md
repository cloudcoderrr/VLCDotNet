# VLC 4 native build patches

Unified-diff patches in this directory are applied to the VLC 4 source checkout
before the contrib and native-runtime phases of full-source native builds.
[build/lib.sh](../../build/lib.sh) applies them in lexical order using `git apply`,
with a `patch -p1` fallback.

The patches address platform detection, codec builds and native module linkage.
Keep fixes in unified-diff format so they can be reviewed and rebased when the
upstream VLC ref changes. See the [build guide](../../build/README.md).
