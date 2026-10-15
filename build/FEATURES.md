# Native runtime feature scope

Full-source native builds enable VLC's full default contrib set with the
exclusions below. The final plugin set depends on the VLC series, target SDK
and available platform integration libraries. Build scripts are the source of
truth for the exact configuration.

## Media and platform integration

The runtime includes FFmpeg decoding and scaling, codec/demux/subtitle libraries,
image decoding, metadata support and stream output. Supported targets also
include x264/x265 encoding and the desktop DVD/Blu-ray stack.

Linux builds enable desktop display and audio integrations from system
libraries. Linux x64 additionally enables VA-API/VDPAU hardware decoding; Linux
ARM/ARM64 builds disable those backends. Windows and Apple use their native
platform modules; Android uses MediaCodec where available. Hardware support
requires a compatible device and driver.

## Native transport and networking

The baseline contrib configuration retains the networking group, including TLS,
RTSP, RIST, SRT, SMB, NFS, mDNS and UPnP dependencies. HTTP/HTTPS, HLS/DASH, FTP,
TCP/UDP and RTP/SDP use VLC's native transport modules and network-enabled
FFmpeg. Stream output and service discovery are included where supported by
the platform and VLC series.

Apple uses SecureTransport for TLS. Its contrib configuration excludes GnuTLS
and the associated crypto closure, SRT, SSH2, VNC and Shout. iOS and its simulator
additionally exclude librist and NFS. Android excludes the VNC plugin.

See [build-apple.sh](build-apple.sh), [build-android.sh](build-android.sh),
[build-linux.sh](build-linux.sh) and [build-windows.sh](build-windows.sh) for
target-specific enablement.

## Baseline exclusions

`CONTRIB_DEFAULT_PRUNE` in [lib.sh](lib.sh) excludes:

- OpenCV/protobuf computer vision and Chromaprint fingerprinting.
- libplacebo rendering and projectM/goom visualizations.
- Qt GUI libraries, medialibrary and Breakpad.
- Contrib XCB libraries (Linux uses system XCB).
- x262 and libaom encoders; AV1 decoding uses dav1d.
- Lua scripting and CDDB metadata lookup.

These builds produce libvlc runtimes without the VLC GUI player.

## Additional target exclusions

| Target | Additional contrib / module exclusions |
|--------|-----------------------------------------|
| Windows | mpg123; MP3 decoding remains available through mad/FFmpeg. |
| Android | Optical-disc stack and mpg123; VLC 4 also excludes sidplay2. |
| Apple, all | GnuTLS/crypto closure, SRT, SSH2, VNC, Shout and Sparkle. |
| iOS / simulator | Optical-disc stack, Schroedinger, libvpx, libass/Harfbuzz/FriBidi, librist and NFS; VLC 4 also excludes asdcplib and x264/x265. |
| Mac Catalyst | Optical-disc stack, x264/x265, Schroedinger and libvpx; VLC 4 arm64 also excludes mpg123. |
| Apple mobile / Catalyst | AVFoundation capture is disabled; the software x264/x265 exclusions above do not disable VideoToolbox. |

## Verification

The shared consumer suite covers baseline playback, tracks, subtitles, parsing,
transport controls and transcoding. Desktop workflows enable extended format,
image, metadata and encoder tests with `VLC_PARITY_TESTS=1` and native transport
tests with `VLC_NETWORK_TESTS=1`. Mobile workflows enable native transport tests
with the `VLC_NET_TESTS` compile symbol.

Additional modules can be required per RID through `VLC_EXPECTED_MODULES` and
`VLC_EXPECTED_NET_MODULES`. Some optional tests skip when a module is absent;
a passing suite does not establish that every upstream VLC feature is present.
See the [recorded verification matrix](../README.md#recorded-verification-matrix)
for runtime-tested targets and hardware limitations.
