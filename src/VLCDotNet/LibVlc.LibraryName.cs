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
        /// On iOS / Mac Catalyst libvlc ships as a signed, embedded dynamic
        /// framework (<c>VLCDotNet.framework/VLCDotNet</c>, built by
        /// stage_dynamic_vlc), so the DllImport target is that framework binary
        /// name; everywhere else the dynamic library is resolved by the base
        /// name <c>libvlc</c> (libvlc.dll / libvlc.so / libvlc.dylib).
        /// </summary>
#if IOS || MACCATALYST
        internal const string Lib = "VLCDotNet";
#else
        internal const string Lib = "libvlc";
#endif

#if IOS || MACCATALYST
        /// <summary>
        /// Resolves the <see cref="Lib"/> DllImport to the embedded dynamic
        /// framework. .NET for iOS copies VLCDotNet.framework into the app
        /// bundle's Frameworks/ folder and signs it, but a bare-name P/Invoke is
        /// not on the default dlopen search path, so we load it explicitly by its
        /// @rpath / bundle-relative install location (the framework's own
        /// install_name is @rpath/VLCDotNet.framework/VLCDotNet). Registered once
        /// via the type initializer, before the first native call.
        /// </summary>
        static LibVlc()
        {
            NativeLibrary.SetDllImportResolver(typeof(LibVlc).Assembly, ResolveNativeLibrary);
        }

        private static System.IntPtr ResolveNativeLibrary(
            string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != Lib)
                return System.IntPtr.Zero; // fall back to default resolution (e.g. libc "__Internal")

            string[] candidates =
            {
                "@rpath/VLCDotNet.framework/VLCDotNet",
                "@executable_path/Frameworks/VLCDotNet.framework/VLCDotNet",      // iOS layout
                "@executable_path/../Frameworks/VLCDotNet.framework/VLCDotNet",   // Mac Catalyst layout
                "VLCDotNet",
            };
            foreach (string candidate in candidates)
            {
                if (NativeLibrary.TryLoad(candidate, out System.IntPtr handle))
                    return handle;
            }
            return System.IntPtr.Zero;
        }
#endif
    }
}
