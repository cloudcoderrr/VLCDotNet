# Cached libvlc main-feature scope and parity audit

Prepared 2026-10-04. Revised scope: VLC's main native multimedia capabilities for .NET consumers; C# owns general I/O and application services. Exclude native networking, Lua/player services and consumer static linking. Specialist extras are outside mandatory parity. All existing RIDs remain required.

## Revised scope and intentional exclusions

The required feature set is the applicable **main-media capability union** of official VLC, minimal and prebuilt, **minus the approved host-owned exclusions below**. Fingerprinting, computer-vision analysis and other specialist extras listed separately are outside mandatory parity. Compare equivalent functionality rather than demanding every duplicate internal backend or an equal plugin count; distinct required formats/profiles/subtitle/hardware/encoding behavior must still be preserved. Excluded features are removed from the restoration backlog. Historical observations about their absence remain evidence of the current build profile, not requirements to restore them. Native network support may still survive the existing prune and must be explicitly removed/disabled from the cached runtime under the new task.

| Intentionally excluded | Current evidence and scope decision |
|---|---|
| Native network input/output and remote capture | Contrib `--disable-net` plus platform `--disable-live555 --disable-realrtsp --disable-srt --disable-shout --disable-vnc` remove some paths. Live555/RealRTSP, SRT, libssh2/SFTP, libshout and VNC dependencies/modules are no longer restoration requirements. Also exclude HTTP/HTTPS, FTP, SMB/NFS, RTP/UDP/TCP, RIST, SAT>IP, network adaptive-stream acquisition and other native network clients/servers, including localhost transports. The flags above alone do not guarantee their absence. |
| Network discovery, casting and synchronization | Network-group UPnP/microdns, native Bonjour, SAP, podcasts, remote browsing/renderers, Chromecast (contrib `--disable-protobuf`) and network synchronization/control are excluded. Apple staging currently drops `services_discovery` and most `stream_out`; local media/device discovery and local outputs must be considered separately. |
| Online metadata and reporting | CDDB (`--disable-libcddb` on Windows), Internet artwork/metadata/lyrics, scrobbling, telemetry/crash uploads, add-on downloads and update checks are excluded. Local media metadata, embedded artwork and audio-CD playback remain retained capabilities; the host chooses external/folder artwork. |
| Network-only security dependencies | Current scripts disable GnuTLS (`--disable-gnutls`) and Windows/Apple remove GnuTLS/SRT libraries after cache restore. Network TLS, SSH, network credential keystores and authentication/certificate dialogs are excluded. Keep crypto/shared libraries if retained local formats/discs need them. Apple SecureTransport's missing static registration is no longer a restoration gap for this profile. |
| Lua scripting and assets | Contrib `--disable-lua --disable-luac`, libvlc `--disable-lua` and unstaged Lua resources already omit Lua. Script control interfaces, extensions, scripted website/playlist resolvers, metadata/artwork fetchers and discovery are deliberately excluded. C# owns playlist-file import; native media-list APIs and .NET/libvlc playback control remain. |
| Player UI and external control services | Exclude the player executable/UI, HTTP/RC/Telnet/D-Bus interfaces, hotkeys/gesture interfaces, desktop notifications/tray integrations, installers and browser/ActiveX components. Current Apple staging also drops `control`/`notify`; these player/control-service omissions are no longer parity gaps. Keep an inert/headless interface where initialization needs it, media interaction and normal API controls. |
| Application-owned library/file services | Contrib `--disable-medialibrary` on VLC 4 remains intentional. C# owns library databases/indexing/scanning, directory browsing, playlist-file import/export/UI, generic archive extraction and automatic folder-artwork search. Preserve individual local-file/callback input, native `libvlc_media_list` APIs, metadata and embedded artwork. Stream processing needed by retained media/subtitle formats remains required. |
| Ancillary crash reporting/localization | Contrib `--disable-breakpad` and configure `--disable-nls` remain intentional. Keep usable native errors, statistics and logging, while the .NET host owns crash reporting and UI localization. |
| Consumer static linkage | Remove static libvlc/libvlccore/plugin delivery as a target requirement. Current Apple `.a`/`NativeReference`/`ForceLoad`/libvlc `__Internal` integration must migrate to dynamic frameworks; all existing RIDs remain required. |

## Specialist extras outside mandatory parity

| Optional native feature | Current evidence and default scope |
|---|---|
| Audio fingerprinting/identification | Contrib `--disable-chromaprint`; Linux also disables it in libvlc. Official Windows ships `libstream_out_chromaprint_plugin.dll`. Chromaprint/fingerprint output is excluded from the default dependency requirements; ordinary audio metadata and processing remain required. |
| Computer-vision analysis | Contrib `--disable-opencv --disable-opencv4`. OpenCV-based analysis is optional; normal video filters/deinterlacing/overlays remain required. |
| External visualization engines | Contrib `--disable-projectM --disable-goom`; Windows also disables Goom. Official Windows ships these plugins. projectM/Goom are optional; lightweight built-in visualizations remain retained where provided. |
| Advanced multi-output compositions | Current Apple staging drops `video_splitter` and most `stream_out` modules. Video walls/panorama and bridge/mosaic broadcast compositions are optional; ordinary video output, local recording/transcoding and required file/memory output remain retained. |

Do not restore optional dependencies just to match an official plugin count. Incidental modules with no added dependency/resource requirements may remain, but are not mandatory. Uncommon codecs/demuxers/subtitles are still media functions and must not be dropped on this basis; SID remains a required regression fix.

## Host-supplied media and scripting

C# can download files or supply a valid media byte stream through libvlc callbacks. Native networking exclusions also remove VLC's built-in media-protocol orchestration: HLS/DASH adaptation and RTSP/RTP session handling are more than fetching arbitrary bytes. The host must provide additional logic if it wants those protocols; this task does not promise them through a generic Stream or require a new managed network stack. Native media buffering, timestamps, synchronization, parsing and decoding of supported callback-supplied content remain required. [Custom media input](https://github.com/videolan/vlc/blob/3.0.23/include/vlc/libvlc_media.h), [adaptive playlist engine](https://github.com/videolan/vlc/blob/3.0.23/modules/demux/adaptive/PlaylistManager.cpp), [RTSP/RTP implementation](https://github.com/videolan/vlc/blob/3.0.23/modules/access/live555.cpp).

Lua is broader than script control: upstream documents playlist/site parsers, artwork fetchers, interfaces, extensions and discovery. The exclusion intentionally covers all of these Lua features. It does not remove the native C API that .NET uses to control playback. [Official VLC Lua documentation](https://github.com/videolan/vlc/blob/3.0.23/share/lua/README.txt)

Static Apple packaging does mean libvlc/plugins are currently linked into the consumer app. This is an implementation choice, not a requirement inherent to .NET. The replacement must use dynamically linked Apple frameworks with correct embedding/signing/dependencies and updated libvlc bindings; loose third-party iOS `.dylib` files are unsuitable. Plugin objects can live inside a shared framework and still need registration. Intermediate archives used to produce a shared runtime do not constitute consumer static linking. [Apple framework requirements](https://developer.apple.com/library/archive/technotes/tn2435/_index.html), [.NET native references](https://learn.microsoft.com/en-us/dotnet/ios/building-apps/build-items#nativereference)

## Result and evidence limits

The current CI-cached pipeline is not a complete replacement for the required main native media features of official VLC builds, From-source (minimal), or VideoLAN prebuilt. It targets the same package families and 15 RIDs, but package pruning, configure flags, patches, and static plugin staging change its capabilities. The README statement that the pipelines differ only in dependency acquisition is inaccurate.

This audit inventories every explicit capability exclusion found in the cached scripts and relevant patches, identifies additional packaging gaps, and consolidates the earlier SID finding. It does not claim an exhaustive, runtime-verified missing-feature list for all 30 VLC-series/RID combinations. No cached native builds or fresh CI tests were executed. Features selected automatically, system-library fallbacks, transitive dependencies, and hardware-dependent behavior require inspection and testing of the final artifacts.

Evidence levels used below:

- **Disabled**: the libvlc configure invocation explicitly disables a module/backend.
- **Pruned**: contrib defaults remove a package or automatic package group. A transitive dependency or system library can still supply it; inspect the actual closure before declaring the runtime feature absent.
- **Dropped**: Apple static staging omits an entire plugin category or named module, even if it was built.
- **Unverified**: source evidence identifies a risk but does not prove an end-to-end feature is missing.

Feature parity must be established per OS, architecture, and VLC version. A Linux capture backend is not required on Windows; a differently named upstream replacement can satisfy a capability when behavior is demonstrated. A failed build, CI resource limit, or test-pack omission does not make a feature inapplicable.

## Reference builds

| Reference | Version/ref and evidence |
|---|---|
| Repository VLC 3 cached build | `3.0.23`; current build/contrib workflows and cache manifests. |
| Repository VLC 4 cached build | `e77e49b5dff7484dbf70cd1540a6a568e1890280`; the current default of both cached VLC 4 workflows and all 15 cache manifests. `Directory.Build.props` separately lists `master`, so use the actual workflow/cache ref rather than assuming the property pins it. |
| Official VLC 3 Windows x64 binary | VideoLAN's `vlc-3.0.23-win64.zip`, downloaded and checked against its published SHA-256. **363 plugin DLL paths** were inventoried without executing binaries. |
| Other official platform builds | VideoLAN's platform-specific VLC/libvlc/VLCKit/libvlcjni artifacts and matching build recipes must be recorded and compared in the implementation task. Linux package choice and mobile wrapper versions need explicit provenance. |
| Official VLC 4 comparison | Use a matching pinned nightly/source build; a VLC 3 stable release is not a valid binary baseline for VLC 4. |
| Other repository pipelines | Their checked-in scripts/configuration were inspected. Their final runtime/plugin inventories were not compared. Align VLC refs when creating the parity baselines and record any version differences. |

Official archive SHA-256: `992d19dbd0b8a7cde9167d2f7780b1ef6f92acc8a71acfa736101a21f35181e1`.

The observed official Windows plugin paths are saved in [audit/official-vlc3-win-x64-plugins.txt](audit/official-vlc3-win-x64-plugins.txt). This is evidence of modules shipped by that official build, not proof that every feature operates on every machine.

VideoLAN describes native libvlc as supporting optical-disc playback/menu navigation, hardware decoding, HDR, audio passthrough, filters and immersive media, as well as network browsing and remote renderers. The local multimedia families remain relevant; network/remote-renderer families are deliberately excluded by the revised scope. [Official libvlc capabilities](https://images.videolan.org/vlc/libvlc.html)

## Consolidated comparison with the other two pipelines

| Area | Finding |
|---|---|
| Managed package and target coverage | All three pipelines use the common packing project. Cached workflows configure VLC 3/4 builds for the same 15 RIDs. This establishes intended coverage, not successful runtime parity. |
| Shared tests | The pipelines exercise the shared local-media playback/parse/track/subtitle/control/logging and FFmpeg transcode suite. Passing that suite does not verify every optional module. |
| Confirmed minimal-pipeline difference | For VLC 3, `build/lib.sh` requests `--enable-sid`, and `build/build-windows.sh` passes it into the Windows build. Cached Windows explicitly passes `--disable-sid` for both x64 and ARM64. The official Windows x64 archive also ships `demux/libsid_plugin.dll`. The ARM64 `__chkstk` link failure is documented in the cached script and must be fixed rather than disabling SID on both architectures. |
| Windows subtitle-provider difference | Minimal carries `patches/vlc-3.0/0017-contrib-ass-directwrite-on-desktop-windows.patch`; cached does not. Cached may use another libass font provider, so typography/system-font parity is unverified rather than a confirmed loss of ASS playback. |
| Prebuilt dependency difference | Prebuilt Linux uses distro FFmpeg and other development packages; cross-platform scripts attempt official contrib bundles with source fallback. Cached uses a different, pruned source closure. Compare actual codec/backend/module inventories and behavior; do not assume either closure contains everything in the other. |
| Apple linkage and plugin comparison | All three currently use selective static plugin staging. Retained local plugin omissions remain gaps; the revised cached target must also replace consumer static linkage with dynamic frameworks. Matching another pipeline's static packaging would not satisfy that target. |

Evidence: [minimal codec flags](../build/lib.sh), [minimal Windows build](../build/build-windows.sh), [cached Windows build](build/build-windows.sh), [prebuilt Linux dependencies](../prebuilt/build/deps-linux.sh), [shared tests](../Tests/VLCDotNet.Tests.Shared/VlcTestSuite.cs).

## Retained gaps and their native capability impact

The following is the source/configuration inventory for required main media features after removing host-owned exclusions and optional specialist extras. Entries describe a specific removed backend/path; a missing alternative backend becomes a mandatory gap only if required capability equivalence is not demonstrated. “All” means the cached default contrib profile for both VLC series, subject to version-specific availability and dependency resolution. Filters, subtitles, capture, disc menus, hardware integration and encoding remain useful native engine functions.

| Capability/backend | Affected cached targets | Evidence and current limitation |
|---|---|---|
| DVD reading and DVD menu navigation | All default contrib builds; explicitly disabled on Windows | **Pruned/disabled**: `--disable-disc`, Windows `--disable-dvdread --disable-dvdnav`. DVD dependencies/menu paths need restoration. Official Windows ships both plugins. |
| Blu-ray playback, Blu-ray ISO/menu paths and applicable BD-J support | All | **Pruned/disabled**: disc group plus `--disable-bluray` in every platform script. Official Windows ships `liblibbluray_plugin.dll`. Match the official platform's runtime prerequisites and supported disc modes; do not promise access to every protected disc. |
| Audio CD/VCD/SVCD input on Apple | macOS, iOS/simulators, Catalyst | **Disabled**: Apple `--disable-vcd` disables upstream built-in VCD and CD-DA support. Availability is platform-dependent. Other platforms' built-in CD/VCD paths may survive the contrib disc prune, so they are not universally declared absent. |
| Crypto dependencies shared with retained local formats/discs | All default contrib profiles, if needed by a retained reference capability | **Pruned**: `--disable-gcrypt --disable-gpg-error`. The original network adaptive-encryption restoration requirement is removed. Verify whether retained local encrypted-media/disc paths need these libraries and retain only the necessary shared closure; do not infer that every crypto library is network-only. |
| TagLib metadata read/write backend | All | **Pruned/disabled**: `--disable-taglib` in contrib and every platform configure. Official Windows ships TagLib. Other demuxers can still expose metadata, but TagLib-dependent editing/saving paths need restoration. |
| x264 H.264 encoder, including applicable 10-bit backend | All default contrib profiles | **Pruned**: `--disable-x264`. Official Windows ships x264 and x26410b plugins. H.264 decoding can remain available through FFmpeg/hardware. |
| x265 HEVC encoder | All default contrib profiles | **Pruned**: `--disable-x265`. Official Windows ships x265. HEVC decoding can remain available. |
| x262 MPEG-2 encoder backend | All default contrib profiles | **Pruned**: `--disable-x262`. This backend is not present in the inspected official Windows archive; treat it as an excluded optional backend and require it only where a reference exposes it. MPEG-2 decoding/other encoding paths may remain. |
| libaom AV1 backend | All default contrib profiles | **Pruned**: `--disable-aom`. Official VLC 3 Windows ships an AOM plugin; upstream VLC 3 configures it as an AV1 decoder. The cached comments describe it only as an encoder, which is incomplete. Dav1d/FFmpeg may still decode AV1. |
| mpg123 MPEG audio decoder backend | All default contrib profiles | **Pruned**: `--disable-mpg123`. Official Windows ships mpg123. MP3 playback may remain through FFmpeg/MAD. |
| liba52 AC-3 decoder backend | Linux, Apple, Android | **Disabled**: `--disable-a52`. AC-3 can remain through FFmpeg. This backend is optional upstream and is not automatically a gap in every official platform baseline. |
| SID music decoder | Windows VLC 3; VLC 4 default contrib profile | **Disabled/pruned**: Windows `--disable-sid`; VLC 4 appends `--disable-sidplay2` globally. For VLC 3 Windows this is the confirmed difference against minimal and the official binary. Check VLC 4's supported replacement before assigning its requirement. |
| Schroedinger Dirac backend | iOS/simulators and Catalyst | **Pruned**: Apple `--disable-schroedinger`. FFmpeg may still decode Dirac. |
| libvpx VP8/VP9 decode/encode backend | iOS/simulators and Catalyst | **Pruned**: Apple `--disable-vpx`. VP8/VP9 decoding may still use another path; libvpx encoding is a distinct capability. |
| libass styled ASS/SSA rendering | iOS and simulators | **Pruned/disabled**: `--disable-ass` plus configure `--disable-libass`. Text subtitle parsing and basic display can remain while styled rendering differs. |
| HarfBuzz shaping and FriBidi bidirectional subtitle processing | iOS and simulators | **Pruned/disabled**: `--disable-harfbuzz --disable-fribidi` in contrib and configure. Verify complex scripts, RTL text, font fallback, embedded fonts and subtitle fidelity. |
| Fontconfig-based font provider | Windows configure | **Disabled**: `--disable-fontconfig`. Native Windows providers may replace it; the different minimal libass DirectWrite patch makes font-provider behavior a verification item, not a claim that all subtitle fonts are unsupported. |
| XML-dependent media/subtitle parsing | Apple and Android | **Disabled**: `--disable-libxml2`. Determine retained format consumers and replacement readers per VLC series; restore required XML functionality without restoring host-owned playlist-file services. Current Apple staging also drops `misc`/stream processing categories. |
| Native Linux ALSA and PulseAudio output/device paths | Linux | **Disabled**: `--disable-alsa --disable-pulse`. Callback audio tests do not validate normal device output or passthrough. Android also disables these Linux backends, which may be inapplicable there. |
| Linux XCB/X11 output/window integration | Linux | **Pruned/disabled**: `--disable-xcb`. The managed `set_xwindow` binding does not restore a missing native output. Android also disables XCB, generally inapplicable there. |
| VDPAU acceleration backend | Linux | **Disabled**: `--disable-vdpau`; Android also disables it. This does not prove that D3D11, VideoToolbox, MediaCodec or other GPU paths are absent. |
| SDL_image image-decoding backend | Linux and Android | **Disabled**: `--disable-sdl-image`. Other image decoders can remain. Compare the exact formats/backends available in the official target build. |
| Screen capture | Apple | **Disabled**: `--disable-screen`. macOS is the primary applicable comparison; mobile restrictions require a target-specific implementation/app integration assessment. |
| AVFoundation capture backend | iOS/simulators and Catalyst | **Disabled**: `--disable-macosx-avfoundation`. Validate against applicable upstream support. This flag does not disable VideoToolbox decoding. |
| V4L2 capture backend | Android | **Disabled**: `--disable-v4l2`. Usually not the Android app camera path; this is an explicit backend exclusion requiring applicability evidence rather than a blanket camera-support claim. Linux's capture support remains unverified because the runner installs only tools, not all system development packages. |
| ZVBI teletext backend | Windows | **Disabled**: `--disable-zvbi`. Official Windows ships `libzvbi_plugin.dll`; distinguish this backend from other DVB/teletext/subtitle paths. |
| ARIB STD-B25 stream-filter backend | Windows | **Disabled**: `--disable-aribcam`. Official Windows ships `libaribcam_plugin.dll`. Static Apple filters also omit this category. |
| libplacebo-backed rendering/filtering | All default contrib profiles | **Pruned**: `--disable-libplacebo`. Exact module availability differs by VLC series and platform. HDR/tone mapping through other renderers may remain. |
| Core Image video-filter path on VLC 4 Catalyst | Catalyst VLC 4 | **Patch exclusion**: `0004-apple-skip-ci-filters-on-maccatalyst.patch` removes the iOS/EAGL path. Preserve platform-correct replacements where supported; Catalyst lacks the iOS OpenGLES API, so simply reverting the patch is not a fix. |
| iOS OpenGLES/EAGL video-output paths on Catalyst | Catalyst VLC 3/4 | **Patch exclusion**: Catalyst-specific patches skip incompatible iOS output modules/keys. Match equivalent rendering/decoding behavior using supported Catalyst APIs instead of requiring an unavailable platform backend. |
| AS-DCP/Digital Cinema backend and its selected dependencies | iOS/simulators VLC 4 | **Pruned**: `--disable-asdcplib --disable-nettle --disable-gmp` added for these targets. Retain required local Digital Cinema/crypto functionality when the applicable reference exposes it; network-only uses are excluded. |

Primary repository evidence: [default prune](build/lib.sh), [Windows configure/restoration](build/build-windows.sh), [Linux configure](build/build-linux.sh), [Apple prune/configure/restoration](build/build-apple.sh), [Android configure](build/build-android.sh), [cached patches](patches).

Upstream mappings were checked in [VLC 3.0.23 configure](https://github.com/videolan/vlc/blob/3.0.23/configure.ac), [contrib rules](https://github.com/videolan/vlc/tree/3.0.23/contrib/src), [adaptive common encryption](https://github.com/videolan/vlc/blob/3.0.23/modules/demux/adaptive/encryption/CommonEncryption.cpp), and [the pinned VLC 4 configure](https://github.com/videolan/vlc/blob/e77e49b5dff7484dbf70cd1540a6a568e1890280/configure.ac). VideoLAN's [3.0.23 release description](https://images.videolan.org/vlc/releases/3.0.23.html) also identifies casting, disc menus, network browsing, encoding, GPU paths and filters as platform-dependent capability families.

## Current Apple packaging gaps and required dynamic migration

The current `stage_static_vlc` registers only `access`, `audio_filter`, `audio_output`, `codec`, `demux`, `packetizer`, `spu`, `text_renderer`, `video_chroma`, and `video_output`, plus file output and two stream-output plugins. Everything else is omitted on all five iOS/simulator/Catalyst RIDs in both series. The revised task replaces static consumer delivery with dynamic frameworks and must preserve all retained modules in the new packaging. This table explains which current omissions still matter; it does not request restoration of static consumer linking. [Staging implementation](build/lib.sh), [Apple configure](build/build-apple.sh), [libvlc import name](../src/VLCDotNet/LibVlc.LibraryName.cs), [VLC 3 consumer targets](../src/VLCDotNet/build/VLCDotNet3.targets), [VLC 4 consumer targets](../src/VLCDotNet/build/VLCDotNet4.targets)

| Currently dropped categories/modules | Retained gap or intentional exclusion |
|---|---|
| `video_filter` | Built-in deinterlace, adjust, crop/pad, rotate/transform, sharpen, scene snapshots, denoising, color/effect filters and applicable hardware/Core Image filter plugins. Dropping this category affects features independently of OpenCV/libplacebo pruning. |
| `video_splitter` | Clone/video-wall/panorama compositions are optional specialist features; this category is outside mandatory restoration. |
| `visualization` | Retain required lightweight built-in visual/spectrum paths. External projectM/Goom engines are optional. |
| `stream_filter` | Retain local cache/prefetch, inflate/decompression, record and other required local stream-processing paths. Network-specific filters/acquisition remain excluded. |
| `stream_extractor` | Generic archive extraction is host-owned file preparation and no longer a mandatory native gap. Preserve separate demuxers/native parsing for retained media containers. |
| `services_discovery` | Generic directory/media browsing and network service discovery are host-owned/excluded. Preserve device enumeration needed for native output/capture through the relevant platform backends/APIs. |
| `meta_engine` | Retain media metadata and embedded-artwork functionality, plus TagLib where needed for saving. Folder-artwork searches and Internet fetchers are host-owned/excluded. |
| `keystore` | Network credential-storage plugins are intentionally excluded. Preserve any proven shared prerequisite for a retained local capability rather than restoring the entire category. |
| `misc` | Retain XML and other components required by main media/subtitle formats. Fingerprinting is optional; SecureTransport/network TLS, RTSP VOD and scrobbling are excluded. |
| `logger` | Native logger backend plugins in series/builds that place them here. Application log callbacks are a separate path and can still work. |
| `audio_mixer` | Float/integer audio mixer plugins. Do not assume callback audio tests cover every device/mixing mode. |
| `mux` | Built-in MP4, TS/PS, Ogg, AVI, ASF, WAV and other compiled muxers. The retained FFmpeg avformat path does not establish equivalence to all these muxers. |
| `access_output`, except `access_output_file` | Retain required non-network outputs such as dummy output alongside file output. HTTP, UDP, live HTTP/HLS, SRT/shout/RIST and other network outputs are intentionally excluded. |
| `stream_out`, except `stream_out_standard` and `stream_out_transcode` | Retain required local recording/transcoding/file/memory output and modules needed to deliver it. Bridge/mosaic compositions and fingerprint output are optional; RTP/casting and other network output are excluded. Classify other paths by their main-media capability rather than restoring the entire category. |

The staging allowlist also excludes GUI, Lua and player-control/notification categories; those exclusions are consistent with the revised scope. The required dynamic migration must retain local engine modules independently of how their objects are built or registered. The plugin examples above come from the matched upstream [module build recipes](https://github.com/videolan/vlc/blob/3.0.23/modules/Makefile.am) and their per-category makefiles. Some modules are platform/version dependent; the dropped category is the established packaging fact.

## Additional verification gaps

- **Runtime resources:** `normalize_output` stages libraries/plugins/notices, without general VLC data assets. Retained features may require fonts/font configuration, schemas or other local assets. Package the real retained runtime closure; Lua scripts, network certificate assets and player/application-service resources are excluded.
- **System dependencies:** cached Linux's dependency installer installs compilers/tools, without the complete desktop/capture/GPU development-library set. Auto-detection may silently omit Wayland/OpenGL/VA-API, V4L2, device discovery and other modules. They are unverified, not all proven missing.
- **Bindings/integration:** custom input is essential when C# owns I/O. The repository exposes VLC 3 callback delegates and a raw VLC 4 callback-struct pointer, but no custom media-input usage was found in the checked-in tests. Provide usable version-correct types/helpers or a documented Stream adapter, and test partial reads, seekable/non-seekable sources, EOF/errors, stop/cancellation, GC/lifetime and concurrent instances. Audit retained capture/output, metadata, disc and hardware APIs too. Network authentication/certificate integration is excluded. Apple `__Internal` libvlc bindings/targets/resource lookup still need dynamic migration and clean consuming-app tests. [Current media imports](../src/VLCDotNet/LibVlc.Media.cs), [callback delegates](../src/VLCDotNet/Callbacks.cs), [pinned VLC 4 input ABI](https://github.com/videolan/vlc/blob/e77e49b5dff7484dbf70cd1540a6a568e1890280/include/vlc/libvlc_media.h).
- **Hardware/advanced media:** D3D11/DXVA2, VideoToolbox, MediaCodec, VA-API, HDR/tone mapping, zero-copy, HDMI/SPDIF passthrough, 360-degree video and ambisonics require explicit validation. They must not be labelled missing merely because the synthetic tests do not exercise them.
- **Exclusion precision:** `--disable-net` changes contrib selection, not every core socket module. VLC 3 NFS is independently selected, Windows can have native SMB, and alternate TLS/discovery/core transport paths may survive. Audit actual packaged modules and reachable native transports, including FFmpeg/other dependency paths, and disable/remove all excluded native networking. Keep local device/capture paths and app-supplied callbacks independently of those exclusions.
- **Tests and target execution:** the current cached workflows execute desktop tests for `linux-x64`, `win-x64`, `osx-arm64` and mobile tests for `android-x64`, `iossimulator-arm64`, `maccatalyst-arm64`. The 15 build targets are not 15 executed runtime test targets. Reduced Windows/Apple-mobile/VLC-4 required-module lists allow missing optional modules to go unnoticed.
- **Cache provenance:** manifests record version/ref, hash and original prefix, but not the effective feature/prune profile, patch hash, dependency inventory, compiler/SDK versions or runner identity. Restore verifies the archive hash but does not enforce all these compatibility dimensions. Existing cache contents cannot be fully described by today's flags alone.

## Existing player exclusions and linkage facts

`--disable-vlc`, `--disable-qt`, `--disable-skins2`, `--disable-macosx`, `--disable-ncurses`, `--disable-update-check` and `--disable-sparkle` remove standalone-player components. Pruned GUI packages include `--disable-qtdeclarative --disable-qtshadertools --disable-qtsvg --disable-qtwayland`. These options and application components remain intentionally excluded. `--disable-shared` / `--disable-static` choose target linkage rather than multimedia capability, but the revised user requirement explicitly selects dynamic consumer linkage. Existing static Apple delivery therefore needs migration. No new OS/architecture family was requested; all existing 15 RIDs remain covered.

## Follow-on task

[FEATURE-PARITY-TASK.md](FEATURE-PARITY-TASK.md) defines the main-feature union, host/native responsibility boundary, exclusions/optional extras, dynamic packaging migration and acceptance checks. The required gaps above are the starting backlog. Baseline/artifact comparison must discover and resolve missing main capabilities, demonstrate reliable .NET-supplied input and verify that excluded features are disabled before claiming completion. These documents revise the task; no native scripts, binaries or packages have been changed by the audit.
