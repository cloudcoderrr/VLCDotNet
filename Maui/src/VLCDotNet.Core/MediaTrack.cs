using System;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    /// <summary>
    /// Immutable managed description of one elementary stream (audio, video or
    /// subtitle track), decoded from the native <c>libvlc_media_track_t</c>.
    /// </summary>
    public sealed class MediaTrack
    {
        internal MediaTrack(in VlcMediaTrack raw)
        {
            Type = raw.Type;
            Codec = raw.Codec;
            OriginalFourcc = raw.OriginalFourcc;
            Id = raw.Id;
            Profile = raw.Profile;
            Level = raw.Level;
            Bitrate = raw.Bitrate;
            Language = LibVlc.Utf8ToString(raw.Language);
            Description = LibVlc.Utf8ToString(raw.Description);
            CodecDescription = LibVlc.Utf8ToString(LibVlc.libvlc_media_get_codec_description(raw.Type, raw.Codec));

            if (raw.TrackData != IntPtr.Zero)
            {
                switch (raw.Type)
                {
                    case VlcTrackType.Audio:
                        var a = Marshal.PtrToStructure<VlcAudioTrack>(raw.TrackData);
                        Channels = a.Channels;
                        SampleRate = a.Rate;
                        break;
                    case VlcTrackType.Video:
                        var v = Marshal.PtrToStructure<VlcVideoTrack>(raw.TrackData);
                        Width = v.Width;
                        Height = v.Height;
                        FrameRateNumerator = v.FrameRateNum;
                        FrameRateDenominator = v.FrameRateDen;
                        SampleAspectRatioNumerator = v.SarNum;
                        SampleAspectRatioDenominator = v.SarDen;
                        Orientation = v.Orientation;
                        break;
                }
            }
        }

        /// <summary>The kind of elementary stream.</summary>
        public VlcTrackType Type { get; }

        /// <summary>FourCC codec identifier.</summary>
        public uint Codec { get; }

        public uint OriginalFourcc { get; }

        /// <summary>Elementary-stream id used for track selection.</summary>
        public int Id { get; }

        public int Profile { get; }

        public int Level { get; }

        /// <summary>Bitrate in bits per second, when known.</summary>
        public uint Bitrate { get; }

        /// <summary>BCP-47 / ISO language tag, when known.</summary>
        public string? Language { get; }

        public string? Description { get; }

        /// <summary>Human-readable codec name (e.g. "H264 - MPEG-4 AVC (part 10)").</summary>
        public string? CodecDescription { get; }

        // Video
        public uint Width { get; }
        public uint Height { get; }
        public uint FrameRateNumerator { get; }
        public uint FrameRateDenominator { get; }
        public uint SampleAspectRatioNumerator { get; }
        public uint SampleAspectRatioDenominator { get; }
        public VlcVideoOrient Orientation { get; }

        // Audio
        public uint Channels { get; }
        public uint SampleRate { get; }

        /// <summary>Approximate frames per second for a video track, or 0.</summary>
        public double FrameRate =>
            FrameRateDenominator == 0 ? 0 : (double)FrameRateNumerator / FrameRateDenominator;

        public override string ToString() => Type switch
        {
            VlcTrackType.Video => $"Video #{Id} {Width}x{Height} @ {FrameRate:0.##}fps {CodecDescription}",
            VlcTrackType.Audio => $"Audio #{Id} {Channels}ch {SampleRate}Hz {CodecDescription} [{Language}]",
            VlcTrackType.Text => $"Subtitle #{Id} {CodecDescription} [{Language}]",
            _ => $"Track #{Id} {CodecDescription}",
        };
    }
}
