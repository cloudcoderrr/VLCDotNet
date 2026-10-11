using System;
using System.Collections.Generic;

namespace VLCDotNet.Tests.Shared
{
    /// <summary>Expected characteristics of one test media file (from the pack MANIFEST).</summary>
    public sealed class MediaSpec
    {
        public MediaSpec(string fileName)
        {
            FileName = fileName;
        }

        public string FileName { get; }

        public bool HasVideo { get; init; }

        public bool HasAudio { get; init; }

        public int VideoWidth { get; init; }

        public int VideoHeight { get; init; }

        public int AudioChannels { get; init; }

        public int AudioSampleRate { get; init; }

        public int AudioTrackCount { get; init; } = 1;

        public int SubtitleTrackCount { get; init; }

        /// <summary>Approximate duration in milliseconds (for sanity checks).</summary>
        public int DurationMs { get; init; }

        public string? VideoCodecHint { get; init; }

        public string? AudioCodecHint { get; init; }

        public IReadOnlyList<string> ExpectedModuleMarkers { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> ExpectedAppleModuleMarkers { get; init; } = Array.Empty<string>();
    }

    /// <summary>The synthetic media pack under Tests/Data/vlc_test_pack and its expectations.</summary>
    public static class MediaCatalog
    {
        public static readonly MediaSpec TransportStream = new MediaSpec("video_h264_aac.ts")
        {
            HasVideo = true, HasAudio = true, VideoWidth = 640, VideoHeight = 360,
            AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 8021,
            VideoCodecHint = "h264", AudioCodecHint = "mp4a",
        };

        public static readonly IReadOnlyList<MediaSpec> Videos = new List<MediaSpec>
        {
            new MediaSpec("video_h264_aac.mp4")
            {
                HasVideo = true, HasAudio = true, VideoWidth = 640, VideoHeight = 360,
                AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 8000,
                VideoCodecHint = "h264", AudioCodecHint = "mp4a",
            },
            TransportStream,
            new MediaSpec("video_vp9_opus.webm")
            {
                HasVideo = true, HasAudio = true, VideoWidth = 640, VideoHeight = 360,
                AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 8008,
                VideoCodecHint = "vp09", AudioCodecHint = "opus",
            },
            new MediaSpec("video_mpeg4_mp3.avi")
            {
                HasVideo = true, HasAudio = true, VideoWidth = 640, VideoHeight = 360,
                AudioChannels = 1, AudioSampleRate = 44100, DurationMs = 8045,
                VideoCodecHint = "mp4v", AudioCodecHint = "mpga",
            },
            new MediaSpec("video_hevc_aac.mkv")
            {
                HasVideo = true, HasAudio = true, VideoWidth = 640, VideoHeight = 360,
                AudioChannels = 1, AudioSampleRate = 48000, DurationMs = 6021,
                VideoCodecHint = "hevc", AudioCodecHint = "mp4a",
            },
        };

        public static readonly MediaSpec DiracFlac = new MediaSpec("video_dirac_flac.mkv")
        {
            HasVideo = true, HasAudio = true, VideoWidth = 640, VideoHeight = 360,
            AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 6000,
            VideoCodecHint = "drac", AudioCodecHint = "flac",
        };

        public static readonly IReadOnlyList<MediaSpec> AppleHardwareDecodeVideos = new List<MediaSpec>
        {
            Videos[0],
            Videos[4],
        };

        /// <summary>Container with two audio and two subtitle tracks for switch testing.</summary>
        public static readonly MediaSpec Multitrack = new MediaSpec("multitrack_h264_audio_subs.mkv")
        {
            HasVideo = true, HasAudio = true, VideoWidth = 640, VideoHeight = 360,
            AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 7807,
            AudioTrackCount = 2, SubtitleTrackCount = 2,
            VideoCodecHint = "h264",
        };

        public static readonly IReadOnlyList<MediaSpec> Audios = new List<MediaSpec>
        {
            new MediaSpec("audio_pcm_stereo.wav")   { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "araw" },
            new MediaSpec("audio_mp3_stereo.mp3")   { HasAudio = true, AudioChannels = 2, AudioSampleRate = 44100, DurationMs = 5041, AudioCodecHint = "mpga", ExpectedModuleMarkers = new[] { "mad", "mpg123" } },
            new MediaSpec("audio_flac_stereo.flac") { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "flac", ExpectedModuleMarkers = new[] { "flac" } },
            new MediaSpec("audio_aac_stereo.m4a")   { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "mp4a", ExpectedModuleMarkers = new[] { "faad" } },
            new MediaSpec("audio_opus_stereo.ogg")  { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5006, AudioCodecHint = "opus" },
        };

        /// <summary>
        /// Extra containers/codecs enabled by the feature-parity contrib closure
        /// (see cached/FEATURE-PARITY-TASK.md). Only exercised when the parity
        /// profile is active (VLC_PARITY_TESTS); other pipelines skip them.
        /// </summary>
        public static readonly IReadOnlyList<MediaSpec> ExtraVideos = new List<MediaSpec>
        {
            new MediaSpec("video_mpeg2_ps.mpg")  { HasVideo = true, HasAudio = true, VideoWidth = 640, VideoHeight = 360, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 6000, VideoCodecHint = "mpgv", AudioCodecHint = "mpga" },
            new MediaSpec("video_mpeg1_mp2.mpg") { HasVideo = true, HasAudio = true, VideoWidth = 640, VideoHeight = 360, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 6000, VideoCodecHint = "mp1v", AudioCodecHint = "mpga" },
            new MediaSpec("video_mjpeg_pcm.avi") { HasVideo = true, HasAudio = true, VideoWidth = 640, VideoHeight = 360, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 3000, VideoCodecHint = "mjpg", AudioCodecHint = "araw" },
            new MediaSpec("video_wmv2_wma.wmv")  { HasVideo = true, HasAudio = true, VideoWidth = 640, VideoHeight = 360, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 6000, VideoCodecHint = "wmv2", AudioCodecHint = "wma2" },
            new MediaSpec("video_h264_aac.mov")  { HasVideo = true, HasAudio = true, VideoWidth = 640, VideoHeight = 360, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 8000, VideoCodecHint = "h264", AudioCodecHint = "mp4a" },
            new MediaSpec("video_h263_aac.3gp")  { HasVideo = true, HasAudio = true, VideoWidth = 352, VideoHeight = 288, AudioChannels = 1, AudioSampleRate = 48000, DurationMs = 6000, VideoCodecHint = "h263", AudioCodecHint = "mp4a" },
        };

        /// <summary>Extra audio codecs enabled by the feature-parity contrib closure.</summary>
        public static readonly IReadOnlyList<MediaSpec> ExtraAudios = new List<MediaSpec>
        {
            new MediaSpec("audio_ac3_stereo.ac3")    { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "a52" },
            new MediaSpec("audio_eac3_stereo.eac3")  { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "eac3" },
            new MediaSpec("audio_dts_stereo.dts")    { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "dts" },
            new MediaSpec("audio_alac_stereo.m4a")   { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "alac" },
            new MediaSpec("audio_wavpack_stereo.wv") { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "WV" },
            new MediaSpec("audio_vorbis_stereo.ogg") { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "vorb" },
            new MediaSpec("audio_mp2_stereo.mp2")    { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "mpga" },
            new MediaSpec("audio_wma_stereo.wma")    { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "wma2" },
            new MediaSpec("audio_g711_mulaw.wav")    { HasAudio = true, AudioChannels = 1, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "ulaw" },
            new MediaSpec("audio_tta_stereo.tta")    { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "TTA1" },
            new MediaSpec("audio_pcm_s16be.aiff")    { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "twos" },
            new MediaSpec("audio_pcm_s16le.caf")     { HasAudio = true, AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "araw" },
            new MediaSpec("audio_pcm_s16be.au")      { HasAudio = true, AudioChannels = 1, AudioSampleRate = 48000, DurationMs = 5000, AudioCodecHint = "twos" },
        };

        /// <summary>Still images for the image-decode parity tests.</summary>
        public static readonly IReadOnlyList<string> Images = new List<string>
        {
            "image_test.png",
            "image_test.jpg",
            "image_test.bmp",
            "image_test.gif",
            "image_test.tiff",
        };

        /// <summary>Audio file carrying title/artist/album tags for the metadata (taglib) test.</summary>
        public static readonly MediaSpec TaggedAudio = new MediaSpec("audio_tagged.mp3")
        {
            HasAudio = true, AudioChannels = 2, AudioSampleRate = 44100, DurationMs = 5000, AudioCodecHint = "mpga",
        };


        /// <summary>External subtitle files to attach as slaves.</summary>
        public static readonly IReadOnlyList<string> ExternalSubtitles = new List<string>
        {
            "subtitles_en.srt",
            "subtitles_es.vtt",
            "subtitles_styled.ass",
        };
    }
}
