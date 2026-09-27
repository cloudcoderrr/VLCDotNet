using System;

namespace VLCDotNet
{
    // Android-only partial. Compiled solely for the net*-android target framework
    // (see VLCDotNet.csproj), so the Java interop types are available here and the
    // shared VlcRuntime.cs stays free of Android references. This avoids relying on
    // the ANDROID preprocessor symbol, which the mobile CI build drops when it
    // overrides DefineConstants (-p:DefineConstants=AUTORUN;VLC4).
    public static partial class VlcRuntime
    {
        private static bool s_androidCoreLoaded;

        // VLC 4.0's libvlccore captures the JavaVM in its JNI_OnLoad, which the
        // Android runtime only invokes when the library is loaded through
        // java.lang.System.loadLibrary. .NET resolves native libraries with dlopen
        // (which does not call JNI_OnLoad), so libvlc_new would abort in
        // system_Configure on "s_jvm != NULL". Load the C++ runtime and libvlccore
        // explicitly so JNI_OnLoad runs and registers the JavaVM before the first
        // P/Invoke into libvlc.
        static partial void LoadAndroidCoreLibraries()
        {
            if (s_androidCoreLoaded)
            {
                return;
            }

            s_androidCoreLoaded = true;
            foreach (string lib in new[] { "c++_shared", "vlccore" })
            {
                try
                {
                    Java.Lang.JavaSystem.LoadLibrary(lib);
                    Android.Util.Log.Info("VLCDotNet", "System.loadLibrary(" + lib + ") succeeded");
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Warn("VLCDotNet", "System.loadLibrary(" + lib + ") failed: " + ex.Message);
                }
            }
        }
    }
}
