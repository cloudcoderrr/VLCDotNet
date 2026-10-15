#!/usr/bin/env bash
#
# Generate the EXTRA synthetic test media that exercises the feature-parity
# codecs/containers enabled by full-source native builds (see build/FEATURES.md). All clips are fully synthetic (lavfi testsrc2 video + sine audio),
# mirroring the original vlc_test_pack so visual/audio defects stay obvious.
#
# Requires a full FFmpeg build on PATH (libx264/libtheora/libvorbis/libmp3lame/
# libwebp). Files that a given FFmpeg cannot encode are skipped with a warning;
# the test suite gates each format on the decoder/module actually being present,
# so a missing optional file never fails a run.
#
# Usage:  bash Tests/Data/generate_extra_media.sh [output-dir]
#         (default output dir: Tests/Data/vlc_test_pack)
#
set -u
HERE="$( cd "$(dirname "${BASH_SOURCE[0]}")" && pwd )"
OUT="${1:-${HERE}/vlc_test_pack}"
mkdir -p "${OUT}"
cd "${OUT}"

FF="${FFMPEG:-ffmpeg}"
VDUR="${VDUR:-6}"
ADUR="${ADUR:-5}"
VSRC="testsrc2=size=640x360:rate=25:duration=${VDUR}"
ASRC="sine=frequency=440:sample_rate=48000:duration=${ADUR}"
# Second tone so stereo files have distinguishable channels for track tests.
ASRC2="sine=frequency=660:sample_rate=48000:duration=${ADUR}"

made=0; skipped=0
# gen <outfile> <ffmpeg-args...>
gen() {
  local out="$1"; shift
  if "${FF}" -hide_banner -loglevel error -y "$@" "${out}" 2>/tmp/ffgen.err; then
    if [ -s "${out}" ]; then
      printf '  [ok]   %s (%s bytes)\n' "${out}" "$(wc -c < "${out}" | tr -d ' ')"
      made=$((made+1)); return 0
    fi
  fi
  printf '  [skip] %s -- %s\n' "${out}" "$(tail -n1 /tmp/ffgen.err 2>/dev/null | tr -d '\n')"
  rm -f "${out}"; skipped=$((skipped+1)); return 1
}

echo "== stereo audio bed (two tones) =="
STEREO=(-f lavfi -i "${ASRC}" -f lavfi -i "${ASRC2}" -filter_complex "[0:a][1:a]join=inputs=2:channel_layout=stereo[a]" -map "[a]")
MONO=(-f lavfi -i "${ASRC}")
AV=(-f lavfi -i "${VSRC}" -f lavfi -i "${ASRC}" -f lavfi -i "${ASRC2}" -filter_complex "[1:a][2:a]join=inputs=2:channel_layout=stereo[a]" -map 0:v -map "[a]")

echo "== extra audio codecs =="
gen audio_ac3_stereo.ac3      "${STEREO[@]}" -c:a ac3   -b:a 192k
gen audio_eac3_stereo.eac3    "${STEREO[@]}" -c:a eac3  -b:a 192k
gen audio_dts_stereo.dts      "${STEREO[@]}" -c:a dca   -strict -2
gen audio_alac_stereo.m4a     "${STEREO[@]}" -c:a alac
gen audio_wavpack_stereo.wv   "${STEREO[@]}" -c:a wavpack
gen audio_vorbis_stereo.ogg   "${STEREO[@]}" -c:a libvorbis -q:a 4
gen audio_mp2_stereo.mp2      "${STEREO[@]}" -c:a mp2   -b:a 192k
gen audio_wma_stereo.wma      "${STEREO[@]}" -c:a wmav2 -b:a 128k
gen audio_g711_mulaw.wav      "${MONO[@]}"   -c:a pcm_mulaw
gen audio_tta_stereo.tta      "${STEREO[@]}" -c:a tta
gen audio_pcm_s16be.aiff      "${STEREO[@]}" -c:a pcm_s16be
gen audio_pcm_s16le.caf       "${STEREO[@]}" -c:a pcm_s16le
gen audio_pcm_s16be.au        "${MONO[@]}"   -c:a pcm_s16be

echo "== metadata (taglib) =="
gen audio_tagged.mp3          "${STEREO[@]}" -c:a libmp3lame -b:a 192k \
    -metadata title="VLCDotNet Parity" -metadata artist="Synthetic Tone" \
    -metadata album="Feature Parity Pack" -metadata track="7" -metadata date="2026"

echo "== extra containers / video codecs =="
gen video_mpeg2_ps.mpg        "${AV[@]}" -c:v mpeg2video -b:v 2M -c:a mp2 -b:a 192k -f vob
gen video_mpeg1_mp2.mpg       "${AV[@]}" -c:v mpeg1video -b:v 1500k -c:a mp2 -b:a 192k -f mpeg
gen video_theora_vorbis.ogv   "${AV[@]}" -c:v libtheora -q:v 7 -c:a libvorbis -q:a 4
gen video_mjpeg_pcm.avi       "${AV[@]}" -c:v mjpeg -q:v 3 -c:a pcm_s16le
gen video_wmv2_wma.wmv        "${AV[@]}" -c:v wmv2 -b:v 1M -c:a wmav2 -b:a 128k
gen video_h264_aac.mov        "${AV[@]}" -c:v libx264 -pix_fmt yuv420p -preset ultrafast -c:a aac -b:a 128k
# H.263 requires a standard frame size (CIF 352x288); rescale for this clip only.
gen video_h263_aac.3gp        -f lavfi -i "testsrc2=size=352x288:rate=25:duration=${VDUR}" \
    -f lavfi -i "${ASRC}" -c:v h263 -c:a aac -b:a 96k -ar 48000
gen video_dv_pcm.dv           -f lavfi -i "testsrc2=size=720x480:rate=30000/1001:duration=${VDUR}" \
    -f lavfi -i "${ASRC}" -ac 2 -c:v dvvideo -pix_fmt yuv411p -aspect 4:3 -c:a pcm_s16le

echo "== still images =="
IMG=(-f lavfi -i "testsrc2=size=320x240" -frames:v 1)
gen image_test.png            "${IMG[@]}"
gen image_test.jpg            "${IMG[@]}" -q:v 3
gen image_test.bmp            "${IMG[@]}"
gen image_test.gif            "${IMG[@]}"
gen image_test.tiff           "${IMG[@]}"
gen image_test.webp           "${IMG[@]}" -c:v libwebp

echo
echo "Generated ${made} file(s), skipped ${skipped}."
