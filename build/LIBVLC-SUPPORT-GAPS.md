# libVLC support gaps in the current builds

Audit date: 2026-10-16, America/Los_Angeles. Repository HEAD: `c7dcaf50b2dd6d32728d833b51fb4ae4e0344ee7`.

The largest practical gaps are Chromecast on every target; SMB in VLC 3 outside Windows; XML-dependent playback, archive access, credential storage and advanced subtitles on Apple mobile; and native AES-encrypted HLS in VLC 3 on Apple. VLC 4 also has useful native APIs that the C# bindings do not expose.

## Scope and evidence

This compares the latest successful native outputs, rather than assuming that every configure flag or README statement describes the delivered binaries:

- VLC 3: **3.0.23**, [build 38957839989](https://github.com/cloudcoderrr/VLCDotNet/actions/runs/38957839989), repository build commit `da02c45d`.
- VLC 4: **4.0 development commit `e77e49b5`**, [build 38971187505](https://github.com/cloudcoderrr/VLCDotNet/actions/runs/38971187505), repository build commit `349b9072`.
- All **30 series/RID outputs** were inspected through their archive file lists; Apple embedded modules were read from the actual `static-modules.txt` registration manifests. Build logs were also checked for capabilities that have no separate plugin, particularly libgcrypt and text shaping.
- Official Windows ZIP inventories were compared directly: 3.0.23, current stable **3.0.24**, and VLC 4 nightlies at the same `e77e49b5` commit. For Linux, macOS and mobile, the baseline is upstream platform/package configuration and relevant source, rather than an exact binary inventory for every official package. Mac Catalyst has no separate official binary baseline in this audit.

The [evidence appendix](../artifacts/libvlc-audit-20261016/EVIDENCE.md) contains every RID, artifact ID, module-presence matrix and filtered Windows difference. Raw inventories and representative/all-Apple build logs are alongside it.

**Evidence limit:** module absence establishes that a particular implementation is not delivered. Module presence does not prove hardware, network, DRM, or device support at runtime. The lists below are confirmed omissions and source-derived consequences; this is not an exhaustive runtime certification against every official distribution.

VLC application UI, Lua and other script control, service control, hotkeys, gestures, update systems, addon repositories, debugging modules, toy effects, obsolete hardware/codecs and experimental examples are excluded. VLC 4 is compared at the project's pinned commit; newer unreleased work is not counted as a missing production feature.

## Native gaps by platform

Rows apply to both VLC series unless a version is specified. Common omissions in the next section also apply to each row.

| Platform and all shipped RIDs | Missing or reduced capabilities |
|---|---|
| Windows: `win-x64`, `win-arm64` | Common Chromecast and optional processing omissions below. **x64 only:** the official OpenGL/WGL output and its interop modules are absent in both series. VLC 4 also lacks the libplacebo/Vulkan rendering path. Official ARM64 references themselves omit the x64 OpenGL stack, so that is not an ARM64 gap. Direct3D output and D3D11 hardware decoding remain packaged. |
| Linux: `linux-x64`, `linux-arm`, `linux-arm64` | **VLC 3:** SMB share playback and SoundFont MIDI synthesis. **Both:** JACK audio/capture, MTP media access and udev-backed device discovery. **VLC 4:** native Wayland output/window modules. **ARM/ARM64 only:** librsvg SVG rendering/decoding, Secret Service credential storage and the VDPAU backend are absent; x64 includes those providers. VA-API modules are present on all three architectures; their runtime effectiveness on ARM was not established. |
| macOS: `osx-x64`, `osx-arm64` | **Both:** SRT, SFTP and Icecast/Shout publishing. **VLC 3:** SMB and the native adaptive player's AES-128 HLS decryption. DVD/Blu-ray, camera/audio capture, desktop capture, XML, archives and Keychain are present. |
| iOS: `ios-arm64`; simulators: `iossimulator-x64`, `iossimulator-arm64` | **Both:** libass ASS/SSA styling, FriBidi/HarfBuzz text shaping; XML-dependent playback; archive playback/extraction; libVLC keystore backends; NFS; libvpx VP8/VP9 encoding; CoreImage filter module. **VLC 3:** SMB and native AES-128 HLS decryption. **VLC 4:** RIST and the x265 software encoder. SRT/SFTP/Shout are also absent locally, but are not treated as mobile parity requirements where the official Apple library profile omits them too. |
| Mac Catalyst: `maccatalyst-x64`, `maccatalyst-arm64` | **Both:** XML-dependent playback, archive extraction, libVLC keystore backends, libvpx VP8/VP9 encoding and x264/x265 software encoding. **VLC 3:** SMB, native AES-128 HLS decryption, H.264/H.265 encoding without an available bundled encoder, and the native iOS OpenGLES video output. VLC 4 has VideoToolbox encoding and sample-buffer display modules. SRT/SFTP/Shout and desktop capture inputs are absent locally; these are platform limitations, not differences established against an official Catalyst binary. |
| Android: `android-arm`, `android-arm64`, `android-x64` | **VLC 3:** SMB share playback and SoundFont MIDI synthesis. Both series lack Chromecast and libplacebo-based processing. MediaCodec, native Android video/audio outputs, advanced subtitles, XML, archives, DVD/Blu-ray image modules and the main network transports remain packaged. |

The target scope above covers all **15 RIDs**. No tvOS or visionOS runtime/package head is supplied here, although upstream Apple builds support those targets. They would require separate platform work if C# support for them is desired; legacy CPU slices are outside this comparison.

## What the native gaps mean

| Capability | Affected outputs | Evidence and practical effect |
|---|---|---|
| Chromecast playback/casting | All 30 | No `stream_out_chromecast` or `demux_chromecast`; protobuf is pruned in `build/lib.sh`. A bound `libvlc_media_player_set_renderer` does not supply the missing Cast implementation. UPnP/mDNS discovery being present is not evidence that Cast output works. |
| SMB network shares | VLC 3: every non-Windows RID | No `smb`, `smb2`, `dsm` or Windows UNC provider. VLC 3 upstream defaults SMB2 **off**, and these scripts never enable it. Windows VLC 3 uses native Windows SMB. VLC 4 has SMB2 on non-Windows targets and Windows UNC access. This is not a blanket SMB gap in VLC 4. |
| AES-128 encrypted HLS through native adaptive input | VLC 3: all seven Apple RIDs | Every Apple VLC 3 configure log reports `gcrypt ... no`. Upstream `CommonEncryption.cpp` returns no decrypted bytes for encrypted segments without `HAVE_GCRYPT`. HTTPS through SecureTransport does not supply segment decryption. This is a source-derived failure of the native path; FFmpeg fallback was not tested. All seven VLC 4 Apple logs report gcrypt **yes**, so this omission does not apply to them. |
| DASH / Smooth Streaming and XML playlists/subtitles | Both series: iOS, simulators, Catalyst | No registered `xml` module; the Apple script disables libxml2 outside macOS. The adaptive XML parser requires `xml_ReaderCreate`, affecting native DASH and Smooth manifests. XSPF/other XML playlist readers and XML subtitle paths such as TTML/USF also lose their parser. HLS M3U8 and ordinary SRT/WebVTT are separate paths. These consequences follow from source dependencies, not a complete stream test. |
| Full ASS/SSA rendering | Both series: iOS and simulators | `--disable-libass`; no `libass` module. The generic subtitle decoder remains, so some ASS text may display, but libass typography, positioning and animation fidelity are not available. Catalyst retains libass. |
| Bidirectional and complex-script subtitle shaping | Both series: iOS and simulators | Explicit `--disable-fribidi --disable-harfbuzz`. FreeType and Apple font lookup remain, but the normal FreeType shaping/bidi providers are missing. Do not equate this with no Unicode text or no subtitles. |
| Playback of media inside archives | Both series: iOS, simulators, Catalyst | No registered `archive` stream extractor; that category is omitted by Apple framework staging. A contrib archive existing on disk does not make its plugin available inside the framework. |
| libVLC credential storage | Both series: iOS, simulators, Catalyst | No registered `keychain`, `file_keystore` or `memory_keystore`; staging omits the keystore category. Host-supplied credentials may still work, but libVLC's normal credential caching/persistence is missing. |
| NFS playback | Both series: iOS and simulators | No `nfs` module; contrib is pruned. macOS, Catalyst, Android, Windows and Linux retain NFS. |
| RIST input/output | VLC 4: iOS and simulators | No `rist` or `access_output_rist`; librist is pruned. VLC 3's native RIST modules are present on these same RIDs, but implement the older feature set. |
| SRT input/output | Both series: macOS; also absent on all Apple mobile outputs | Apple prunes SRT and passes `--disable-srt`. Count it as a desktop omission against full desktop VLC. The upstream Apple library profile also disables SRT, so mobile absence is not independently a parity defect. |
| SFTP input | Both series: macOS; also absent on all Apple mobile outputs | Apple prunes ssh2; no `sftp` module. Desktop Windows/Linux and Android retain it. Apply the official platform profile when deciding mobile parity. |
| Icecast/Shout publishing | Both series: macOS; also absent on all Apple mobile outputs | Apple prunes shout; no `access_output_shout`. Official macOS desktop configuration explicitly enables Shout. This is outgoing publishing; playing an HTTP radio stream is a separate supported path. |
| SoundFont MIDI synthesis | VLC 3: Linux and Android | No FluidSynth/FluidLite codec module. VLC 4 includes `fluidsynth` on those targets. Windows has FluidSynth and Apple has AudioToolbox MIDI, so neither is a general MIDI gap. |
| JACK audio output/input | Both series: all Linux RIDs | No JACK output/capture module; the Linux dependency setup omits JACK headers. Upstream Linux packaging includes JACK development dependencies. ALSA/PulseAudio and custom audio callbacks remain available. |
| MTP media access and udev device discovery | Both series: all Linux RIDs | No MTP/udev modules; corresponding development packages are not installed by this build. Manual file/device access and V4L2 capture are separate and remain packaged. |
| Native Wayland output | VLC 4: all Linux RIDs | No Wayland display/window modules. Build logs find Wayland scanner 1.20 and protocols 1.25, below this commit's required 1.23/1.33. X11/XWayland and custom video callbacks are alternatives. VLC 3 labels its Wayland implementation incomplete and defaults it off; that older path is excluded from production gaps. |
| SVG provider | Both series: `linux-arm`, `linux-arm64` | No `svg` or `svgdec`; target librsvg/cairo development libraries are missing. Linux x64 includes both. This is an SVG-specific omission, not general image decoding. |
| Desktop Secret Service credential backend | Both series: `linux-arm`, `linux-arm64` | No `secret` module. File/memory keystores remain; Linux x64 includes Secret Service. This is provider parity, rather than loss of all credential storage. |
| VDPAU hardware decoding/display/filtering | Both series: `linux-arm`, `linux-arm64` | No VDPAU modules; explicitly disabled for those architectures. This is relevant only to hardware/driver combinations supporting VDPAU. VA-API modules are present and require separate device verification. |
| OpenGL/WGL presentation and interop | Both series: `win-x64` | Official matched x64 builds contain `gl`, `glwin32`, `wgl`, `glinterop_dxva2`; local output does not. VLC 4 additionally lacks the OpenGL filter/offscreen chain and NVDEC GL interop found in that reference. Direct3D remains; this does not mean Windows hardware decode or HDR is universally missing. |
| libplacebo rendering/scaling | VLC 4: all RIDs lack its modules | `placebo`, `placebo_gl`, `placebo_vk` and `pl_scale` are not delivered; libplacebo is pruned. This removes that provider's advanced scaling, color/tone-mapping and shader path where official platform builds expose it. Platform-native rendering can still provide HDR/color processing. Vulkan/window support is also absent from the Windows references' local equivalents. |
| VP8/VP9 software encoding via libvpx | Both series: iOS, simulators, Catalyst | No `vpx` encoder/decoder module; libvpx is pruned and FFmpeg contrib disables its libvpx wrapper. VP8/VP9 playback can still use FFmpeg or hardware. VLC 4's `vpx_alpha` is an alpha reconstruction module and does not supply the missing encoder. |
| x264/x265 software encoders | Both series: Catalyst; VLC 4: x265 additionally absent on iOS/simulators | Catalyst has neither module. VLC 3 also has no VideoToolbox encoder, so bundled H.264/H.265 encoding is missing there. VLC 4 has `videotoolbox_enc`; iOS VLC 4 retains x264 but lacks x265. This is partly a local capability limitation: upstream non-GPL Apple library profiles do not promise x264/x265 either. |
| AV1 encoding through libaom | VLC 4: all RIDs lack `aom` | No bundled libaom encoder; rav1e is also removed by policy, but that experimental encoder is not a requested parity target. AV1 decoding remains through dav1d. Official iOS itself disables aom, so do not count AV1 encoding as an iOS-specific official gap. VLC 3's AOM module is decoder-only; its absence does not establish an encoding gap. |
| CoreImage video filters | Both series: iOS/simulators | `ci_filters` is absent from the embedded registration list. macOS includes it. Catalyst's OpenGLES-dependent implementation is excluded for SDK compatibility and is not counted as a supported-Catalyst parity target. Basic software adjustment/deinterlace/crop/rotation/blending are registered on current Apple mobile outputs. |
| Audio fingerprint generation | VLC 3: all RIDs; VLC 4: all except iOS/simulators | No `stream_out_chromaprint` on those outputs. A generic `fingerprinter` plugin alone does not supply the missing output processor. Count this as an optional desktop feature; official mobile profiles also normally prune chromaprint. Current VLC 4 iOS frameworks actually retain `stream_out_chromaprint` despite the baseline prune. |

Optional audio visualization output is also deliberately omitted: GOOM, projectM and the policy-removed spectrum/visual modules. These can be used by a C# host through libVLC, but are a separate optional media effect, and official mobile builds omit several too. Toy video effects are excluded from this report.

### Missing C# access to native functionality

These are **binding gaps**, not reasons to rebuild native libVLC. Additional correct P/Invoke declarations can expose them. They apply wherever the underlying native function/platform is supported.

| Series | Useful missing managed surface |
|---|---|
| 3 and 4 | Host authentication/question/progress callbacks and responses: `libvlc_dialog_*`. This is how a C# application handles libVLC authentication; it does not require the VLC player UI. |
| 3 and 4 | Structured title/chapter descriptions and their release functions; chapter count for another title; native previous/next chapter helpers; player audio role. Basic chapter selection already exists. |
| 3 and 4, Android | `libvlc_media_player_set_android_context`. Native Android output modules exist, but the public managed surface does not expose this context setup function. Custom decoded-frame/audio callbacks are a separate path. |
| 4 | GPU output callbacks, `libvlc_video_set_output_callbacks`, for host-managed graphics integration. CPU video callbacks remain bound. |
| 4 | New crop border/ratio/window and display-fit APIs; projection/stereo mode controls; subtitle text scale and teletext transparency. |
| 4 | Audio mix-mode selection; program enumeration/selection and multiple-track selection. |
| 4 | Native A/B loops; next-media chaining; recording start/stop; relative time jumps; previous-frame stepping. Existing time/seek/sout operations cover parts of these use cases, not the corresponding native API. |
| 4 | New parser/thumbnail tasks, picture handling/saving, downloader task APIs, file statistics and extra metadata access. |
| 4 | Time watching/interpolation and native player synchronization APIs. |
| 4 | Discoverer destruction and renderer-item retain functions. These are important lifetime-management omissions for consumers using discovery. |

The raw header/export name comparison finds **55 non-deprecated-header functions unbound in VLC 3 and 105 in VLC 4**. These are not feature counts: they include alternate getters, application UI, old library/VLM surfaces and other excluded functions. The raw lists are retained only in the evidence JSON; the table above filters for this task. Name coverage is not full ABI validation.

One concrete ABI defect was found: [`libvlc_video_set_deinterlace`](../src/VLCDotNet/LibVlc.Video.cs) is declared with the VLC 3 `(player, mode)` signature for both packages. The pinned VLC 4 header requires `(player, state, mode)` and returns `int`. Thus the packaged native deinterlace filter is present, but that C# import cannot safely call it in VLC 4. Native capability and safe managed access must be tracked separately.

## Additional gaps against today's stable VLC 3.0.24

The project still targets 3.0.23. The [official 3.0.24 NEWS](https://raw.githubusercontent.com/videolan/vlc/3.0.24/NEWS) identifies these relevant additions, independently of configure/module exclusions:

- FFmpeg 8.1-based codec coverage, including the newly listed APV and ATRAC3/ATRAC9 support. A codec already playable through another 3.0.23 path should not be considered wholly unsupported merely because it appears in this release note.
- CEA-708 closed captions carried in MP4.
- ID3v2 metadata exposure in MPEG demuxing.
- RIST main-profile support and the librist-based input/output implementation, beyond the older native simple-profile implementation.
- SRT listener mode on platforms with SRT enabled.
- SFTP public-key authentication options and ED25519 host-key support on platforms with SFTP enabled.
- Updated SMB2 share enumeration.
- NV12 DirectShow capture on Windows, and Moore Threads GPU super-resolution support on applicable hardware.

3.0.24 also contains parser, audio, subtitle, device and security fixes absent from an unmodified 3.0.23 base. Those are maintenance differences rather than unsupported feature categories. Its UI, scripting, updater, installer and obsolete RealRTSP differences are excluded here. These release additions are **not** automatically asserted missing from the separately built VLC 4 commit.

## Important non-gaps and qualifications

- MP3, AC-3/E-AC-3, MPEG-2 and AV1 playback must not be marked unsupported merely because mpg123, a52, libmpeg2 or AOM is omitted. Retained FFmpeg/dav1d/other decoders cover these formats.
- Missing GnuTLS on Apple does not mean missing HTTPS: SecureTransport is present. Missing libgcrypt in VLC 3 Apple concerns native encrypted media processing, independently of TLS.
- DVD/Blu-ray modules are delivered on Windows, Linux, macOS and Android. Apple mobile's optical-drive exclusions match its platform profile. Module presence does not supply disc decryption keys or certify every protected commercial disc.
- Current Apple mobile frameworks include `adjust`, `deinterlace`, `transform`, `rotate`, `croppadd`, `canvas`, `fps`, `blend`, and `scale`; the earlier cached audit's blanket absence claim is stale.
- Bonjour substitutes for microdns on Apple. Absence of the `microdns` filename is not absence of mDNS discovery.
- Native PipeWire is absent locally, but the matched upstream VLC 4 Snap configuration explicitly disables it too. PulseAudio compatibility can still work on a PipeWire desktop. It is not a confirmed difference from that official reference.
- No generic missing HDR, zero-copy, hardware decoding, 360 video, DVD playback, ordinary streaming, or subtitles claim is justified by these inventories. Device tests and representative media are needed to assess those paths.

## Main upstream references

- [Official stable download directory](https://download.videolan.org/pub/vlc/last/) and [3.0.24 changes](https://raw.githubusercontent.com/videolan/vlc/3.0.24/NEWS).
- [VLC 3 configure defaults](https://github.com/videolan/vlc/blob/3.0.23/configure.ac), [adaptive XML parser](https://github.com/videolan/vlc/blob/3.0.23/modules/demux/adaptive/xml/DOMParser.cpp), and [AES implementation](https://github.com/videolan/vlc/blob/3.0.23/modules/demux/adaptive/encryption/CommonEncryption.cpp).
- [Official Apple VLC 3 library build](https://github.com/videolan/vlckit/blob/3.0/buildMobileVLCKit.sh), [pinned VLC 4 Apple configuration](https://github.com/videolan/vlc/blob/e77e49b5dff7484dbf70cd1540a6a568e1890280/extras/package/apple/build.conf), and [desktop macOS configuration](https://github.com/videolan/vlc/blob/3.0.23/extras/package/macosx/configure.sh).
- [Pinned upstream Linux Snap configuration](https://github.com/videolan/vlc/blob/e77e49b5dff7484dbf70cd1540a6a568e1890280/extras/package/snap/snapcraft.yaml) and [VLC Flatpak manifest](https://github.com/flathub/org.videolan.VLC/blob/master/org.videolan.VLC.yaml).
- [Android project](https://github.com/videolan/vlc-android) and [upstream Android native build project](https://code.videolan.org/videolan/libvlcjni).
- [Pinned libVLC 4 public API](https://github.com/videolan/vlc/blob/e77e49b5dff7484dbf70cd1540a6a568e1890280/include/vlc/libvlc_media_player.h).
