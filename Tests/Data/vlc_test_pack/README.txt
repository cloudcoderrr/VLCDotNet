VLC LOCAL BUILD TEST PACK
=========================
All media in this pack is synthetic and generated locally with FFmpeg.

VIDEO FILES
-----------
video_h264_aac.mp4
  H.264/AVC video, AAC stereo audio, MP4 container.

video_vp9_opus.webm
  VP9 video, Opus stereo audio, WebM container.

video_mpeg4_mp3.avi
  MPEG-4 Part 2 video, MP3 audio, AVI container.

video_hevc_aac.mkv
  H.265/HEVC video, AAC audio, Matroska container.

multitrack_h264_audio_subs.mkv
  H.264 video with TWO selectable audio tracks and TWO selectable subtitle tracks.
  Audio 1: AAC, language=eng
  Audio 2: Opus, language=jpn (language tag is for track-switch testing)
  Subtitle 1: SRT, language=eng
  Subtitle 2: styled ASS

STANDALONE AUDIO
----------------
audio_pcm_stereo.wav
  PCM 16-bit stereo.
audio_mp3_stereo.mp3
  MP3 192 kb/s stereo.
audio_flac_stereo.flac
  Lossless FLAC stereo.
audio_opus_stereo.ogg
  Opus stereo in Ogg.

EXTERNAL SUBTITLES
------------------
subtitles_en.srt
subtitles_es.vtt
subtitles_styled.ass

SUGGESTED VLC CHECKS
--------------------
1. Open each video and verify smooth playback and A/V sync.
2. Seek near the middle and end; pause/resume; change playback rate.
3. Drag subtitles_en.srt onto video_h264_aac.mp4 or add it through Subtitle > Add Subtitle File.
4. Open multitrack_h264_audio_subs.mkv and switch between both Audio tracks.
5. Switch between both Subtitle tracks; verify ASS styling/top alignment and Unicode text.
6. Play each standalone audio file; seek and inspect reported codec/sample rate.
7. Test fullscreen, scaling/aspect ratio, volume/mute, and loop/repeat.
8. Enable VLC logging and verify the logs

Note: These clips intentionally use test patterns and fixed tones, so visual corruption,
missing channels, broken seeking, track-selection problems, or subtitle timing issues are easy to notice.
