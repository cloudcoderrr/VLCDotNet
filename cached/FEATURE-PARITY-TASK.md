# CI-cached libvlc: main-feature parity for .NET

Ship dynamically linked `VLCDotNet3`/`VLCDotNet4` runtimes covering retained official/minimal/prebuilt media capabilities on all 15 RIDs, using CI-cached contribs.

## Required on every RID

All features below are required. IDs reference VLC 3; tested VLC 4/backend equivalents are allowed. Ship dependencies/resources and preserve additional reference-supported media formats.

| Feature | Required support | Reference plugins |
|---|---|---|
| Input | Local files, file descriptors, memory and C# Stream callbacks; attachments and concatenated media | `filesystem attachment access_concat access_imem imem` |
| Playback/API | Parse/play/pause/stop, seek/rate, chapter/title/track selection, delays, volume/mute, media lists, events/errors/statistics and snapshots | Native libvlc APIs |
| Containers | MP4/MOV, MKV/WebM, AVI, ASF, MPEG-TS/PS, Ogg, WAV/AIFF/AU/CAF, FLAC, Musepack, TTA, RealMedia, Digital Cinema/DCP and legacy local formats | `mp4 mkv avi asf ts ps ogg avformat wav aiff au caf flacsys mpc tta real dcp nsv nuv pva ty voc xa` |
| Elementary/media parsing | Raw audio/video/DV, H.264/H.265, MPEG video, VC-1, Dirac, MJPEG, CD graphics and STL | `es h26x rawaud rawvid rawdv mpgv vc1 diracsys mjpeg demux_cdg demux_stl dmxmus` |
| Video decoding | H.264, HEVC, AV1, VP8/VP9, MPEG-1/2/4, WMV/VC-1, DV, MJPEG, Theora and Dirac; reference-supported profiles/bit depths | `avcodec dav1d/aom vpx theora rawvideo schroedinger` |
| Audio decoding | PCM/ADPCM, MP1/2/3, AAC, AC-3/E-AC-3, DTS, TrueHD/MLP, FLAC, ALAC, Vorbis, Opus, Speex, WMA, AMR, APE, WavPack and TTA | `avcodec mpg123/mad faad flac opus vorbis speex araw adpcm lpcm g711` |
| Music synthesis | SID, MOD/XM/IT/S3M, game music and MIDI; required soundfonts | `sid mod gme smf fluidsynth` |
| Images | JPEG, PNG, BMP, GIF, TIFF and WebP; image playback, embedded artwork and snapshots | `image jpeg png avcodec` |
| Text subtitles | SRT, ASS/SSA, WebVTT, TTML, USF, tx3g and STL | `subsdec libass webvtt ttml subsusf substx3g stl` |
| Bitmap/caption subtitles | DVD/VobSub, PGS, DVB, CVD/SVCD, CEA-608/708, teletext, ARIB, Kate and SCTE | `spudec vobsub avcodec dvbsub cvdsub svcdsub cc zvbi aribsub kate scte18 scte27 textst` |
| Fonts/text | Embedded/system fonts, fallback, Unicode, complex scripts and RTL | `freetype libass xml`; HarfBuzz/FriBidi and platform font providers |
| Audio processing | Equalizers, compression, normalization/gain, tempo/pitch, resampling, channel mixing/remapping, surround, binaural/ambisonic audio and supported encoded passthrough | `equalizer param_eq compressor normvol gain scaletempo scaletempo_pitch chorus_flanger spatializer spatialaudio stereo_widen headphone_channel_mixer mono remap karaoke dolby_surround_decoder audio_format samplerate speex_resampler float_mixer integer_mixer simple_channel_mixer trivial_channel_mixer spdif tospdif` |
| Video processing | Deinterlace, color adjustment, crop/pad, rotation, scaling/FPS, sharpening, blur/denoise, overlays, subtitle delay, snapshots and 360-degree viewpoints | `deinterlace adjust croppadd canvas transform rotate scale fps sharpen gaussianblur hqdn3d gradfun postproc antiflicker colorthres extract mirror blend logo marq subsdelay scene`; renderer viewpoint support |
| Pixel/color processing | YUV/RGB conversion, NV12/P010, reference-supported high bit depths, HDR color/metadata handling and SDR fallback | `swscale chain grey_yuv i420_10_p010 i420_nv12 i420_rgb i420_yuy2 i422_i420 i422_yuy2 rv32 yuvp yuy2_i420 yuy2_i422`; architecture/renderer helpers |
| Encoding | H.264/HEVC, VP8/VP9, MPEG-1/2/4, MJPEG, Theora, PCM, AAC, MP2/MP3, FLAC, Vorbis, Opus and Speex | `avcodec x264/x26410b x265 vpx theora flac vorbis opus speex twolame` |
| Local output | Recording/transcoding; MP4/MOV, MKV/WebM, AVI, ASF, TS/PS, Ogg, WAV and MJPEG muxing; file and raw-memory output | `avformat mux_mp4 mux_avi mux_asf mux_ts mux_ps mux_ogg mux_wav mux_mpjpeg access_output_file stream_out_standard stream_out_transcode stream_out_record stream_out_smem` |
| Stream processing | Codec packetization, buffering/prefetch, recording, decompression/tag skipping and applicable local ARIB B25 processing | `packetizer_a52 packetizer_av1 packetizer_copy packetizer_dirac packetizer_dts packetizer_flac packetizer_h264 packetizer_hevc packetizer_mlp packetizer_mpeg4audio packetizer_mpeg4video packetizer_mpegaudio packetizer_mpegvideo packetizer_vc1 cache_block cache_read prefetch record inflate skiptags aribcam` |
| Disc media | DVD/Blu-ray local files/images and supported menu navigation; required local disc crypto | `dvdread dvdnav libbluray`; native menus, keys/prerequisites as in the reference |
| Metadata | Read/write/save media tags, track/duration properties and embedded artwork | `taglib` and native media/demux APIs |
| Embedding/diagnostics | Audio/video callbacks, headless operation, logs/statistics and built-in spectrum/visualization | `amem vmem adummy vdummy dummy logger console_logger file_logger stats visual/glspectrum` |

## Additional native support by platform

Hardware decode, HDR/tone mapping, zero-copy and passthrough require supported devices/outputs; simulators retain software fallback. Capture requires permissions. Physical drives/BD-J apply to reference-supported desktops.

| Platform/RIDs | Required native backends/features |
|---|---|
| Windows: `win-x64`, `win-arm64` | D3D11VA/DXVA2 decoding (`d3d11va dxva2`); D3D11/native-window rendering (`direct3d11 drawable`, supported D3D9 fallback); WASAPI (`mmdevice wasapi`, supported DirectSound fallback); `dshow access_wasapi screen` capture; physical CD/VCD/DVD/Blu-ray (`cdda vcd dvdread dvdnav libbluray`); system fonts/DirectWrite. Fix SID ARM64 `__chkstk` and retain SID on x64. |
| Linux: `linux-x64`, `linux-arm`, `linux-arm64` | VA-API/VDPAU on supported GPUs (`vaapi vaapi_drm vdpau_avcodec` and interop/filter helpers); X11/Wayland and GL/GLES rendering (`xcb_window xcb_x11 gl glx gles2 wl_shm xdg_shell`); ALSA/PulseAudio (`alsa pulse`); `v4l2 access_alsa pulsesrc screen/xcb_screen` capture; physical optical discs; Fontconfig/system fonts. |
| macOS: `osx-x64`, `osx-arm64` | `videotoolbox`; native view/Core Animation/OpenGL rendering (`vout_macosx/caopengllayer gl` and CVPX helpers); CoreAudio (`auhal`); `avcapture avaudiocapture screen` capture; supported physical optical discs/BD-J; native font lookup and applicable Core Image filters. |
| Mac Catalyst: `maccatalyst-x64`, `maccatalyst-arm64` | `videotoolbox`; a Catalyst-compatible native view renderer and AudioUnit/CoreAudio output; native font lookup; capture through supported native backends or host-supplied callbacks. Replace incompatible iOS EAGL/Core Image paths with supported equivalents. |
| iOS: `ios-arm64`, `iossimulator-x64`, `iossimulator-arm64` | Device `videotoolbox` plus simulator software decoding; native view rendering (`vout_ios/gles2` or supported replacement); AudioUnit (`audiounit_ios`), native font lookup; capture through supported native backends or host-supplied callbacks. |
| Android: `android-arm`, `android-arm64`, `android-x64` | `mediacodec` plus software fallback; surface/EGL/GLES rendering (`android_display android_window egl_android gles2` and interop helpers); AudioTrack/OpenSL ES (`android_audiotrack opensles_android`, version-correct alternatives); device audio/video capture through supported native backends or host-supplied callbacks. |

## Exclude from the default native profile

- Media networking: HTTP(S), FTP/SFTP, SMB/NFS, RTSP/RTP/UDP/TCP, SRT/RIST, HLS/DASH acquisition, remote capture, casting/discovery/synchronization, CDDB/online metadata and network credential/TLS/SSH services. C# owns transport/protocol processing. Preserve OS IPC for local audio/video.
- Application services: Lua/scripts/extensions, player GUI/executable, external controls, hotkeys/gestures/notifications, updates/add-ons, telemetry/Breakpad, translation catalogs, library/indexing/directory browsing, playlist-file import/export, archive extraction and folder-art searches. Preserve libvlc controls and required local-media crypto.
- Specialist extras: Chromaprint/fingerprinting, OpenCV, projectM/Goom and video-wall/mosaic compositions. Incidental dependency-free modules may remain.
- Static consumer linking: no app-linked libvlc/plugin archives or libvlc `__Internal`; Apple uses signed embedded dynamic frameworks/XCFramework slices. Build-internal archives are allowed.

## Done when

1. Package both series/all 15 RIDs: VLC 3 `3.0.23`, VLC 4 `e77e49b5dff7484dbf70cd1540a6a568e1890280`. Compare official/minimal/prebuilt inventories; document tested equivalents.
2. NuGets ship/load required plugins, dependencies, fonts/resources and notices. Fix pruning, flags/patches, Apple module registration/loading and consumer targets; rebuild caches keyed by feature/source/patch/toolchain.
3. Test every checklist feature through packaged consumers on every RID, plus hardware/output/capture on applicable devices. Record OS/version limitations with upstream evidence.
4. Validate VLC 3/4 C# input ABIs: seekable/non-seekable, known/unknown length, partial reads, EOF/errors, cancellation, disposal/GC and concurrent players. Excluded network/Lua paths remain disabled; byte callbacks alone do not promise HLS/DASH/RTSP.
5. Clean apps prove dynamic linkage/plugin availability, including Apple device/simulator embedding/signing. Update README's verified matrix; missing/unverified required capabilities prevent completion.

Evidence and detailed gaps: [FEATURE-PARITY-AUDIT.md](FEATURE-PARITY-AUDIT.md).
