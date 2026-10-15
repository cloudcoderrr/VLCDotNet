# Cached Pipeline Feature Restoration

| Modules / features to restore | Targets | VLC |
|---|---|---|
| HTTP/HTTPS input: `http`, `https`; functional TLS provider | All 15 RIDs | 3, 4 |
| Native HTTP-based HLS/DASH fetching | All 15 RIDs | 3, 4 |
| FTP download/upload: `ftp` | All 15 RIDs | 3, 4 |
| TCP/UDP input: `tcp`, `udp` | All 15 RIDs | 3, 4 |
| RTP/SDP input and raw-video RTP encoding: `rtp`, `sdp`, `rtpvideo` | All 15 RIDs | 3, 4 |
| SAT>IP reception: `satip` | All 15 RIDs | 3, 4 |
| MMS input: `access_mms` | All 15 RIDs | 3, 4 |
| RIST input: `rist` | All 15 RIDs | 3, 4 |
| FFmpeg AVIO input/output: `avio`; network-enabled FFmpeg | All 15 RIDs | 3, 4 |
| HTTP streaming output: `access_output_http` | Windows, Linux, macOS, Android | 3, 4 |
| UDP streaming output: `access_output_udp` | Windows, Linux, macOS, Android | 3 |
| RTP streaming output: `stream_out_rtp` | Windows, Linux, macOS, Android | 3, 4 |
| RTSP video-on-demand serving: `vod_rtsp` | Windows, Linux, macOS, Android | 3 |
| RIST output: `access_output_rist` | Windows, Linux, macOS, Android | 3, 4 |
| SAP and podcast discovery: `sap`, `podcast` | Windows, Linux, macOS, Android | 3, 4 |
| Bonjour discovery: `bonjour` | macOS | 3, 4 |
| SMB share access: `smb` (VLC 3), `smb2` (VLC 4) | Windows | 3, 4 |
| BDA TV/radio tuner input: `dtv` | Windows x64 with compatible tuner/driver | 3, 4 |
| DVB TV/radio tuner input: `dtv`, `dvb` | Linux with compatible tuner/driver | 3, 4 |
| VA-API hardware decoding | Linux with supported GPU/driver | 3, 4 |
| libass DirectWrite font provider | Windows | 3 |