namespace VLCDotNet.Maui
{
    /// <summary>
    /// Advisory rendering strategy for <see cref="VideoView"/>. MAUI selects the
    /// best path per platform automatically (hardware-accelerated native output on
    /// Apple, bitmap rendering on Android/Windows); this enum is reserved for
    /// forcing a specific mode in future revisions.
    /// </summary>
    public enum VideoViewRenderMode
    {
        Auto,
        NativeHandle,
        Memory,
    }
}
