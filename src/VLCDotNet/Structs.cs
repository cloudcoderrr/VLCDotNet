using System;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    /// <summary>Audio-specific fields of a media track (<c>libvlc_audio_track_t</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VlcAudioTrack
    {
        public uint Channels;
        public uint Rate;
    }

    /// <summary>3D viewpoint of a video track (<c>libvlc_video_viewpoint_t</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VlcVideoViewpoint
    {
        public float Yaw;
        public float Pitch;
        public float Roll;
        public float FieldOfView;
    }

    /// <summary>Video-specific fields of a media track (<c>libvlc_video_track_t</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VlcVideoTrack
    {
        public uint Height;
        public uint Width;
        public uint SarNum;
        public uint SarDen;
        public uint FrameRateNum;
        public uint FrameRateDen;
        public VlcVideoOrient Orientation;
        public VlcVideoProjection Projection;
        public VlcVideoViewpoint Pose;
        public uint Multiview;
    }

    /// <summary>Subtitle-specific fields of a media track (<c>libvlc_subtitle_track_t</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VlcSubtitleTrack
    {
        /// <summary>Pointer to a NUL-terminated UTF-8 encoding name; read with <see cref="LibVlc.Utf8ToString"/>.</summary>
        public IntPtr Encoding;
    }

    /// <summary>
    /// Description of a single elementary stream (<c>libvlc_media_track_t</c>).
    /// The <see cref="TrackData"/> pointer references a <see cref="VlcAudioTrack"/>,
    /// <see cref="VlcVideoTrack"/> or <see cref="VlcSubtitleTrack"/> depending on
    /// <see cref="Type"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VlcMediaTrack
    {
        public uint Codec;
        public uint OriginalFourcc;
        public int Id;
        public VlcTrackType Type;
        public int Profile;
        public int Level;

        /// <summary>Union pointer to the audio/video/subtitle sub-structure.</summary>
        public IntPtr TrackData;

        public uint Bitrate;

        /// <summary>Pointer to a NUL-terminated UTF-8 language tag; read with <see cref="LibVlc.Utf8ToString"/>.</summary>
        public IntPtr Language;

        /// <summary>Pointer to a NUL-terminated UTF-8 description; read with <see cref="LibVlc.Utf8ToString"/>.</summary>
        public IntPtr Description;
    }

    /// <summary>Linked-list node describing a selectable track (<c>libvlc_track_description_t</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VlcTrackDescription
    {
        public int Id;
        public IntPtr Name;
        public IntPtr Next;
    }

    /// <summary>Linked-list node describing a plugin/module (<c>libvlc_module_description_t</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VlcModuleDescription
    {
        public IntPtr Name;
        public IntPtr ShortName;
        public IntPtr LongName;
        public IntPtr Help;
        public IntPtr Next;
    }

    /// <summary>Linked-list node describing an audio output module (<c>libvlc_audio_output_t</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VlcAudioOutput
    {
        public IntPtr Name;
        public IntPtr Description;
        public IntPtr Next;
    }

    /// <summary>Linked-list node describing an audio output device (<c>libvlc_audio_output_device_t</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VlcAudioOutputDevice
    {
        public IntPtr Next;
        public IntPtr Device;
        public IntPtr Description;
    }

    /// <summary>External input slave descriptor (<c>libvlc_media_slave_t</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VlcMediaSlave
    {
        public IntPtr Uri;
        public VlcMediaSlaveType Type;
        public uint Priority;
    }

    /// <summary>Runtime statistics of a media (<c>libvlc_media_stats_t</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VlcMediaStats
    {
        public int ReadBytes;
        public float InputBitrate;
        public int DemuxReadBytes;
        public float DemuxBitrate;
        public int DemuxCorrupted;
        public int DemuxDiscontinuity;
        public int DecodedVideo;
        public int DecodedAudio;
        public int DisplayedPictures;
        public int LostPictures;
        public int PlayedAbuffers;
        public int LostAbuffers;
        public int SentPackets;
        public int SentBytes;
        public float SendBitrate;
    }
}
