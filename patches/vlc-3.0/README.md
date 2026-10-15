# VLC 3 native build patches

Unified-diff patches in this directory are applied to the VLC 3 source checkout
before the contrib and native-runtime phases of full-source native builds.
[build/lib.sh](../../build/lib.sh) applies top-level patches in lexical order
using `git apply`, with a `patch -p1` fallback. Android ARM additionally applies
patches in [android-arm/](android-arm).

The patches address platform compatibility, codec builds and native module
linkage. Keep fixes in unified-diff format so they can be reviewed and rebased
when the upstream VLC ref changes. See the [build guide](../../build/README.md).
