# Third-party notices

The `VLCDotNet3` / `VLCDotNet4` NuGet packages redistribute native binaries
built from the **VLC media player** sources published by the VideoLAN project.

## VLC / libvlc

- Project: https://www.videolan.org/vlc/ and https://code.videolan.org/videolan/vlc
- The `libvlc` / `libvlccore` libraries are licensed under the
  **GNU Lesser General Public License, version 2.1 or later (LGPL-2.1-or-later)**.
- The VLC application and many of the plugins/contribs bundled here are licensed
  under the **GNU General Public License (GPL)**. These third-party components
  remain under their upstream licenses when redistributed in the NuGet package.
  GPL-only encoders that are unnecessary for playback (e.g. x264/x265) are
  disabled at build time.

A copy of the license text is installed by VLC as `COPYING.LIB` (LGPL) and
`COPYING` (GPL) inside the VLC source tree and is reproduced in the built
artifacts.

## Network / transport components

The cached-contrib pipeline ships VLC's complete native transport/network stack
(see `cached/FEATURE-RESTORATION-TASK.md`), which bundles additional third-party
libraries. Each is redistributed under its own upstream license:

- **GnuTLS** (TLS provider for `http`/`https` and native HLS/DASH) —
  LGPL-2.1-or-later, together with **Nettle** and **GMP**
  (LGPL-3-or-later / GPL-2-or-later) and **libgpg-error** / **libgcrypt**
  (LGPL-2.1-or-later).
- **live555** (RTSP input and RTSP VOD serving) — LGPL-3-or-later.
- **librist** (RIST) — BSD-2-Clause. **libsrt** (SRT) — MPL-2.0.
- **libsmb2** (SMB) and **libnfs** (NFS) — LGPL-2.1-or-later.
- **libmicrodns** (mDNS / Bonjour-style discovery) — LGPL-2.1-or-later.
- **libupnp** (UPnP) — BSD-3-Clause. **libssh2** (SFTP) — BSD-3-Clause.

On Apple platforms TLS is provided by the system **Security.framework**
(SecureTransport) rather than GnuTLS, so the GnuTLS/Nettle/GMP/gcrypt closure is
not bundled there. Each library's full license text ships in the VLC contrib
source tree used to build it.

## Managed bindings

The C# P/Invoke bindings under `src/VLCDotNet` are original work for this
repository and are licensed under the **MIT License**.

The MIT license for those managed bindings does **not** apply to bundled VLC
native binaries, plugins, or other third-party materials. Those files remain
under their respective upstream licenses described above.

## Trademarks

"VLC", "VideoLAN" and the VLC traffic-cone logo are trademarks of the VideoLAN
non-profit organization. This project is **not** affiliated with or endorsed by
VideoLAN. The package does not ship the VLC trademarked artwork.
