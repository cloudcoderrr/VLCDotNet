using System.Runtime.InteropServices;

namespace VLCDotNet
{
    /// <summary>
    /// P/Invoke bindings for the native <c>libvlc</c> C API (VLC 3.0.x).
    /// Methods keep their original C names (e.g. <c>libvlc_new</c>) so they map
    /// 1:1 to the VLC documentation and headers.
    /// </summary>
    public static partial class LibVlc
    {
        /// <summary>
        /// Native library name used by every <see cref="DllImportAttribute"/>.
        /// On iOS libvlc is statically linked into the app binary, so the target
        /// is the special <c>__Internal</c> pseudo-module; everywhere else the
        /// dynamic library is resolved by the base name <c>libvlc</c>
        /// (libvlc.dll / libvlc.so / libvlc.dylib).
        /// </summary>
#if IOS || MACCATALYST
        internal const string Lib = "__Internal";
#else
        internal const string Lib = "libvlc";
#endif
    }
}
