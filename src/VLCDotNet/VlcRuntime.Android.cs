using System;
using System.Runtime.InteropServices;

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

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int JniOnLoadDelegate(IntPtr vm, IntPtr reserved);

        // VLC 4.0's libvlccore captures the JavaVM in its JNI_OnLoad, which
        // libvlc_InternalInit -> system_Configure asserts is non-NULL ("s_jvm !=
        // NULL"). The Android runtime only calls JNI_OnLoad when a library is first
        // loaded through java.lang.System.loadLibrary; .NET resolves native
        // libraries with dlopen (which never calls JNI_OnLoad) and libvlccore is
        // usually already mapped as a transitive dependency by the time this runs,
        // so System.loadLibrary alone is a no-op. Instead, resolve JNI_OnLoad from
        // libvlccore and invoke it with the real JavaVM ourselves. This requires
        // libvlccore to export JNI_OnLoad (see patches/vlc-4.0/0008-*).
        static partial void LoadAndroidCoreLibraries()
        {
            if (s_androidCoreLoaded)
            {
                return;
            }

            s_androidCoreLoaded = true;

            // The C++ runtime backs the C++ VLC plugins (mkv, adaptive, ...).
            TryLoadLibrary("c++_shared");
            TryLoadLibrary("vlccore");
            SeedLibVlcCoreJavaVm();
        }

        private static void TryLoadLibrary(string lib)
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

        private static void SeedLibVlcCoreJavaVm()
        {
            try
            {
                IntPtr jvm = Java.Interop.JniRuntime.CurrentRuntime.InvocationPointer;
                if (jvm == IntPtr.Zero)
                {
                    Android.Util.Log.Warn("VLCDotNet", "JavaVM pointer unavailable; libvlccore JNI not seeded");
                    return;
                }

                IntPtr handle;
                if (!NativeLibrary.TryLoad("libvlccore.so", out handle)
                    && !NativeLibrary.TryLoad("vlccore", out handle))
                {
                    Android.Util.Log.Warn("VLCDotNet", "could not load libvlccore to seed JavaVM");
                    return;
                }

                if (!NativeLibrary.TryGetExport(handle, "JNI_OnLoad", out IntPtr onLoad))
                {
                    Android.Util.Log.Warn("VLCDotNet", "libvlccore does not export JNI_OnLoad");
                    return;
                }

                JniOnLoadDelegate fn = Marshal.GetDelegateForFunctionPointer<JniOnLoadDelegate>(onLoad);
                int version = fn(jvm, IntPtr.Zero);
                Android.Util.Log.Info("VLCDotNet", "libvlccore JNI_OnLoad returned 0x" + version.ToString("x8"));
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("VLCDotNet", "seeding libvlccore JavaVM failed: " + ex);
            }
        }
    }
}

