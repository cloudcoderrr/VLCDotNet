using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace VLCDotNet
{
    /// <summary>
    /// Ensures the native <c>libvlc</c> shared library resolves on Linux and
    /// macOS when it is delivered through the NuGet <c>runtimes/&lt;rid&gt;/native</c>
    /// convention. The default P/Invoke resolver finds <c>libvlc.dll</c> on
    /// Windows and the embedded framework on iOS/Mac Catalyst, but on desktop
    /// Linux/macOS the versioned <c>libvlc.so</c>/<c>libvlc.dylib</c> under the RID
    /// native folder is not always on the default search path. This resolver loads
    /// it by explicit full path. Registered once via a module initializer.
    /// </summary>
    internal static class VlcNativeLibraryResolver
    {
        private static int _registered;

        [ModuleInitializer]
        internal static void Register()
        {
            if (Interlocked.Exchange(ref _registered, 1) != 0)
            {
                return;
            }

            // Windows resolves libvlc.dll fine; iOS/Mac Catalyst already install
            // their own resolver in LibVlc's static constructor (and Android loads
            // libvlc.so via System.loadLibrary). Only desktop Linux/macOS need help.
            if (!(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()))
            {
                return;
            }

            try
            {
                NativeLibrary.SetDllImportResolver(typeof(LibVlc).Assembly, Resolve);
            }
            catch (InvalidOperationException)
            {
                // A resolver is already registered for the assembly; nothing to do.
            }
        }

        /// <summary>The OS-appropriate libvlc file names, most-specific first.</summary>
        private static string[] LibVlcNames => OperatingSystem.IsMacOS()
            ? new[] { "libvlc.dylib", "libvlc.5.dylib" }
            : new[] { "libvlc.so", "libvlc.so.5" };

        /// <summary>
        /// Returns the directory that contains the native libvlc for this OS
        /// (the app base or a <c>runtimes/&lt;rid&gt;/native</c> folder), or
        /// <see langword="null"/> when none is found. Used both to load libvlc and
        /// to locate the sibling <c>plugins</c> directory.
        /// </summary>
        internal static string? FindNativeDirectory()
        {
            string baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
            foreach (string dir in NativeSearchDirectories(baseDir))
            {
                foreach (string name in LibVlcNames)
                {
                    if (File.Exists(Path.Combine(dir, name)))
                    {
                        return dir;
                    }
                }
            }

            return null;
        }

        private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (!string.Equals(libraryName, "libvlc", StringComparison.Ordinal))
            {
                return IntPtr.Zero; // defer other libraries (e.g. libc) to the default resolver
            }

            string? dir = FindNativeDirectory();
            string? lastError = null;
            if (dir is not null)
            {
                foreach (string name in LibVlcNames)
                {
                    string path = Path.Combine(dir, name);
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    try
                    {
                        return NativeLibrary.Load(path);
                    }
                    catch (Exception ex)
                    {
                        lastError = $"{path}: {ex.Message}";
                    }
                }
            }

            // Fall back to the default name-based resolution.
            if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out IntPtr handle))
            {
                return handle;
            }

            if (lastError is not null)
            {
                Console.Error.WriteLine($"[VLCDotNet] could not load native libvlc: {lastError}");
            }

            return IntPtr.Zero;
        }

        private static IEnumerable<string> NativeSearchDirectories(string baseDir)
        {
            yield return baseDir;

            string runtimesRoot = Path.Combine(baseDir, "runtimes");
            if (!Directory.Exists(runtimesRoot))
            {
                yield break;
            }

            // Prefer the RID that matches the running identifier, then any native
            // folder (wrong-OS binaries simply fail to load and are skipped).
            string rid = RuntimeInformation.RuntimeIdentifier;
            string preferred = Path.Combine(runtimesRoot, rid, "native");
            if (Directory.Exists(preferred))
            {
                yield return preferred;
            }

            foreach (string ridDir in Directory.EnumerateDirectories(runtimesRoot))
            {
                string nativeDir = Path.Combine(ridDir, "native");
                if (Directory.Exists(nativeDir) && !string.Equals(nativeDir, preferred, StringComparison.Ordinal))
                {
                    yield return nativeDir;
                }
            }
        }
    }
}
