using System.Collections.Generic;

namespace VLCDotNet.ViewTests
{
    /// <summary>One media file in the shared test pack and what we expect from it.</summary>
    public sealed class MediaItem
    {
        public MediaItem(string fileName, bool hasVideo, bool hasAudio, string? videoCodec = null, string? audioCodec = null)
        {
            FileName = fileName;
            HasVideo = hasVideo;
            HasAudio = hasAudio;
            VideoCodec = videoCodec;
            AudioCodec = audioCodec;
        }

        public string FileName { get; }

        public bool HasVideo { get; }

        public bool HasAudio { get; }

        public string? VideoCodec { get; }

        public string? AudioCodec { get; }
    }

    /// <summary>The subset of Tests/Data/vlc_test_pack exercised through the high-level API.</summary>
    public static class MediaManifest
    {
        /// <summary>Core video formats that must decode and render on every platform.</summary>
        public static readonly IReadOnlyList<MediaItem> Videos = new List<MediaItem>
        {
            new MediaItem("video_h264_aac.mp4", true, true, "h264", "mp4a"),
            new MediaItem("video_h264_aac.ts", true, true, "h264", "mp4a"),
            new MediaItem("video_vp9_opus.webm", true, true, "vp9", "opus"),
            new MediaItem("video_mpeg4_mp3.avi", true, true, "mp4v", "mp3"),
            new MediaItem("video_hevc_aac.mkv", true, true, "hevc", "mp4a"),
        };

        /// <summary>Audio formats that must decode on every platform.</summary>
        public static readonly IReadOnlyList<MediaItem> Audios = new List<MediaItem>
        {
            new MediaItem("audio_pcm_stereo.wav", false, true, audioCodec: "araw"),
            new MediaItem("audio_mp3_stereo.mp3", false, true, audioCodec: "mpga"),
            new MediaItem("audio_flac_stereo.flac", false, true, audioCodec: "flac"),
            new MediaItem("audio_aac_stereo.m4a", false, true, audioCodec: "mp4a"),
            new MediaItem("audio_opus_stereo.ogg", false, true, audioCodec: "opus"),
        };

        /// <summary>A container with two audio and two subtitle tracks.</summary>
        public static readonly MediaItem Multitrack =
            new MediaItem("multitrack_h264_audio_subs.mkv", true, true, "h264");

        /// <summary>The H.264 clip used for the hardware-acceleration test.</summary>
        public static readonly MediaItem HardwareDecodeVideo = Videos[0];

        /// <summary>Every file the MAUI app must stage from its bundled assets.</summary>
        public static IEnumerable<string> AllFileNames()
        {
            foreach (MediaItem v in Videos)
            {
                yield return v.FileName;
            }

            foreach (MediaItem a in Audios)
            {
                yield return a.FileName;
            }

            yield return Multitrack.FileName;
        }
    }
}
