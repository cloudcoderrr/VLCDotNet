using System;

namespace VLCDotNet
{
    /// <summary>Playback time changed (milliseconds).</summary>
    public sealed class MediaPlayerTimeChangedEventArgs : EventArgs
    {
        public MediaPlayerTimeChangedEventArgs(long time) => Time = time;

        public long Time { get; }
    }

    /// <summary>Playback position changed (0.0 - 1.0).</summary>
    public sealed class MediaPlayerPositionChangedEventArgs : EventArgs
    {
        public MediaPlayerPositionChangedEventArgs(float position) => Position = position;

        public float Position { get; }
    }

    /// <summary>Media length changed (milliseconds).</summary>
    public sealed class MediaPlayerLengthChangedEventArgs : EventArgs
    {
        public MediaPlayerLengthChangedEventArgs(long length) => Length = length;

        public long Length { get; }
    }

    /// <summary>Buffering progressed (0 - 100 percent).</summary>
    public sealed class MediaPlayerBufferingEventArgs : EventArgs
    {
        public MediaPlayerBufferingEventArgs(float cache) => Cache = cache;

        public float Cache { get; }
    }

    /// <summary>Number of active video outputs changed.</summary>
    public sealed class MediaPlayerVoutEventArgs : EventArgs
    {
        public MediaPlayerVoutEventArgs(int count) => Count = count;

        public int Count { get; }
    }
}
