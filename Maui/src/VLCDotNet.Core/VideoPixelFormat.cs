namespace VLCDotNet
{
    /// <summary>
    /// 32-bit pixel layout requested from libvlc for in-memory video output.
    /// <see cref="Bgra"/> matches Avalonia's <c>Bgra8888</c> and the Skia/MAUI
    /// default, so it is the natural choice for the view controls.
    /// </summary>
    public enum VideoPixelFormat
    {
        /// <summary>Byte order B, G, R, A.</summary>
        Bgra,

        /// <summary>Byte order R, G, B, A.</summary>
        Rgba,
    }

    internal static class VideoPixelFormatExtensions
    {
        internal static string ToChroma(this VideoPixelFormat format) =>
            format == VideoPixelFormat.Rgba ? "RGBA" : "BGRA";

        internal static (int R, int G, int B, int A) Offsets(this VideoPixelFormat format) =>
            format == VideoPixelFormat.Rgba ? (0, 1, 2, 3) : (2, 1, 0, 3);
    }

    /// <summary>Brightness/colour statistics over a captured frame, used to verify real video rendered.</summary>
    public readonly struct VideoFrameStats
    {
        public VideoFrameStats(long frameCount, double nonDarkFraction, int lumaRange, int avgChannelSpread)
        {
            FrameCount = frameCount;
            NonDarkFraction = nonDarkFraction;
            LumaRange = lumaRange;
            AvgChannelSpread = avgChannelSpread;
        }

        /// <summary>Number of frames displayed so far.</summary>
        public long FrameCount { get; }

        /// <summary>Fraction (0-1) of pixels brighter than a small luma threshold.</summary>
        public double NonDarkFraction { get; }

        /// <summary>Luma spread (brightest minus darkest), a proxy for real image detail.</summary>
        public int LumaRange { get; }

        /// <summary>Average RGB channel spread (high values hint at a colour cast / decode fault).</summary>
        public int AvgChannelSpread { get; }

        /// <summary><see langword="true"/> when the frame looks like genuine decoded video.</summary>
        public bool LooksLikeVideo => FrameCount > 0 && NonDarkFraction > 0.02 && LumaRange > 40;

        public override string ToString() =>
            $"frames={FrameCount}, non-dark={NonDarkFraction:P1}, luma-range={LumaRange}, channel-spread={AvgChannelSpread}";
    }
}
