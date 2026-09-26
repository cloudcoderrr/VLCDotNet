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
                ExpectedAppleModuleMarkers = new[] { "videotoolbox" },
            },
            TransportStream,
            new MediaSpec("video_vp9_opus.webm")
            {
                HasVideo = true, HasAudio = true, VideoWidth = 640, VideoHeight = 360,
                AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 8008,
                VideoCodecHint = "vp09", AudioCodecHint = "opus",
                ExpectedModuleMarkers = new[] { "vpx" },
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
                ExpectedAppleModuleMarkers = new[] { "videotoolbox" },
            },
            new MediaSpec("video_dirac_flac.mkv")
            {
                HasVideo = true, HasAudio = true, VideoWidth = 640, VideoHeight = 360,
                AudioChannels = 2, AudioSampleRate = 48000, DurationMs = 6000,
                VideoCodecHint = "drac", AudioCodecHint = "flac",
                ExpectedModuleMarkers = new[] { "schroedinger" },
            },
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

        /// <summary>External subtitle files to attach as slaves.</summary>
        public static readonly IReadOnlyList<string> ExternalSubtitles = new List<string>
        {
            "subtitles_en.srt",
            "subtitles_es.vtt",
            "subtitles_styled.ass",
        };
    }
}
