# Third-party notices

The `VLCDotNet3` / `VLCDotNet4` NuGet packages redistribute native binaries
built from the **VLC media player** sources published by the VideoLAN project.

## VLC / libvlc

- Project: https://www.videolan.org/vlc/ and https://code.videolan.org/videolan/vlc
- The `libvlc` / `libvlccore` libraries are licensed under the
  **GNU Lesser General Public License, version 2.1 or later (LGPL-2.1-or-later)**.
- The VLC application and some optional plugins are licensed under the
  **GNU General Public License (GPL)**. The binaries shipped in these packages
  are configured to keep the redistributed `libvlc` surface under the LGPL where
  practical (decode-only, no GPL-only encoders). If you enable additional GPL
  plugins in your own build, the resulting distribution is governed by the GPL.

A copy of the license text is installed by VLC as `COPYING.LIB` (LGPL) and
`COPYING` (GPL) inside the VLC source tree and is reproduced in the built
artifacts.

## Managed bindings

The C# P/Invoke bindings under `src/VLCDotNet` are original work for this
repository and are provided under the same LGPL-2.1-or-later terms so the
combined package can be consumed without additional restrictions.

## Trademarks

"VLC", "VideoLAN" and the VLC traffic-cone logo are trademarks of the VideoLAN
non-profit organization. This project is **not** affiliated with or endorsed by
VideoLAN. The package does not ship the VLC trademarked artwork.
