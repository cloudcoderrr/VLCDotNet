using System;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    /// <summary>
    /// Helpers to read fields out of a native <c>libvlc_event_t*</c> passed to a
    /// <see cref="VlcEventCallback"/>. The struct starts with
    /// <c>{ int type; void* p_obj; }</c> followed by an event-specific union, so
    /// the payload offset depends on the pointer size of the running process.
    /// </summary>
    public static class VlcEvent
    {
        /// <summary>Byte offset of the event-specific union payload.</summary>
        public static readonly int PayloadOffset = IntPtr.Size == 8 ? 16 : 8;

        /// <summary>Reads the event type discriminator (<c>libvlc_event_t.type</c>).</summary>
        public static VlcEventType GetEventType(IntPtr evt) => (VlcEventType)Marshal.ReadInt32(evt, 0);

        /// <summary>Reads the object that emitted the event (<c>libvlc_event_t.p_obj</c>).</summary>
        public static IntPtr GetSender(IntPtr evt) => Marshal.ReadIntPtr(evt, IntPtr.Size == 8 ? 8 : 4);

        /// <summary>New playback time in milliseconds (MediaPlayerTimeChanged).</summary>
        public static long ReadNewTime(IntPtr evt) => Marshal.ReadInt64(evt, PayloadOffset);

        /// <summary>New media length in milliseconds (MediaPlayerLengthChanged).</summary>
        public static long ReadNewLength(IntPtr evt) => Marshal.ReadInt64(evt, PayloadOffset);

        /// <summary>New position 0.0-1.0 (MediaPlayerPositionChanged).</summary>
        public static float ReadNewPosition(IntPtr evt) => BitConverter.Int32BitsToSingle(Marshal.ReadInt32(evt, PayloadOffset));

        /// <summary>New cache percentage 0-100 (MediaPlayerBuffering).</summary>
        public static float ReadNewCache(IntPtr evt) => BitConverter.Int32BitsToSingle(Marshal.ReadInt32(evt, PayloadOffset));

        /// <summary>New volume level (MediaPlayerAudioVolume).</summary>
        public static float ReadNewVolume(IntPtr evt) => BitConverter.Int32BitsToSingle(Marshal.ReadInt32(evt, PayloadOffset));

        /// <summary>New media state (MediaStateChanged).</summary>
        public static VlcState ReadNewState(IntPtr evt) => (VlcState)Marshal.ReadInt32(evt, PayloadOffset);

        /// <summary>New parse status (MediaParsedChanged).</summary>
        public static VlcMediaParsedStatus ReadNewParsedStatus(IntPtr evt) => (VlcMediaParsedStatus)Marshal.ReadInt32(evt, PayloadOffset);

        /// <summary>Number of active video outputs (MediaPlayerVout).</summary>
        public static int ReadVoutCount(IntPtr evt) => Marshal.ReadInt32(evt, PayloadOffset);

        /// <summary>Snapshot file path (MediaPlayerSnapshotTaken).</summary>
        public static string? ReadSnapshotFilename(IntPtr evt) => LibVlc.Utf8ToString(Marshal.ReadIntPtr(evt, PayloadOffset));

        /// <summary>Track kind for elementary-stream events (MediaPlayerES*).</summary>
        public static VlcTrackType ReadEsType(IntPtr evt) => (VlcTrackType)Marshal.ReadInt32(evt, PayloadOffset);

        /// <summary>Track id for elementary-stream events (MediaPlayerES*).</summary>
        public static int ReadEsId(IntPtr evt) => Marshal.ReadInt32(evt, PayloadOffset + 4);
    }
}
