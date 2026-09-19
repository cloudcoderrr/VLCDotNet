# Third-party notices

The `VLCDotNet3` / `VLCDotNet4` NuGet packages redistribute native binaries
built from the **VLC media player** sources published by the VideoLAN project.

## VLC / libvlc

- Project: https://www.videolan.org/vlc/ and https://code.videolan.org/videolan/vlc
- The `libvlc` / `libvlccore` libraries are licensed under the
  **GNU Lesser General Public License, version 2.1 or later (LGPL-2.1-or-later)**.
- The VLC application and many of the plugins/contribs bundled here are licensed
  under the **GNU General Public License (GPL)**. Because these packages are
  built from the full VLC source tree (including GPL-licensed components such as
  the FreeType-based text renderer), the **combined redistributable is governed
  by the GPL-2.0-or-later**. GPL-only encoders that are unnecessary for playback
  (e.g. x264/x265) are disabled at build time.

A copy of the license text is installed by VLC as `COPYING.LIB` (LGPL) and
`COPYING` (GPL) inside the VLC source tree and is reproduced in the built
artifacts.

## Managed bindings

The C# P/Invoke bindings under `src/VLCDotNet` are original work for this
repository. Because they are distributed together with the GPL VLC binaries, the
combined `VLCDotNet3` / `VLCDotNet4` packages are distributed under the
**GPL-2.0-or-later**.

## Trademarks

"VLC", "VideoLAN" and the VLC traffic-cone logo are trademarks of the VideoLAN
non-profit organization. This project is **not** affiliated with or endorsed by
VideoLAN. The package does not ship the VLC trademarked artwork.
