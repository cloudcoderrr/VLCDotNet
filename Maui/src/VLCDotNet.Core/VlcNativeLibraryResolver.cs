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

        /// <summary>Candidate libvlc file names for this OS (short symlink names).</summary>
        private static string[] LibVlcNames => OperatingSystem.IsMacOS()
            ? new[] { "libvlc.dylib", "libvlc.5.dylib" }
            : new[] { "libvlc.so", "libvlc.so.5" };

        /// <summary>Glob matching every libvlc variant (incl. the real versioned file).</summary>
        private static string GlobPattern => OperatingSystem.IsMacOS() ? "libvlc.*dylib" : "libvlc.so*";

        /// <summary>
        /// Returns the directory containing the native libvlc for this OS (the app
        /// base or a <c>runtimes/&lt;rid&gt;/native</c> folder), or <see langword="null"/>.
        /// Used to locate the sibling <c>plugins</c> directory.
        /// </summary>
        internal static string? FindNativeDirectory()
        {
            foreach (string path in CandidatePaths())
            {
                return Path.GetDirectoryName(path);
            }

            return null;
        }

        // All libvlc binaries that might be loadable, directory-probed first (RID
        // folders, longest name first) then a recursive sweep of the app base as a
        // fallback for layouts we did not predict.
        private static IEnumerable<string> CandidatePaths()
        {
            string baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (string dir in NativeSearchDirectories(baseDir))
            {
                foreach (string p in LibVlcFilesIn(dir))
                {
                    if (seen.Add(p))
                    {
                        yield return p;
                    }
                }
            }

            IEnumerable<string> recursive;
            try
            {
                recursive = Directory.EnumerateFiles(baseDir, GlobPattern, SearchOption.AllDirectories);
            }
            catch
            {
                recursive = Array.Empty<string>();
            }

            foreach (string p in recursive)
            {
                if (Path.GetFileName(p).StartsWith("libvlc.", StringComparison.Ordinal) && seen.Add(p))
                {
                    yield return p;
                }
            }
        }

        // Every real/symlinked libvlc binary in a directory, longest name first so
        // the real versioned file (never a dangling symlink) is tried before the
        // short symlink names that NuGet pack/restore can leave broken.
        private static List<string> LibVlcFilesIn(string dir)
        {
            var result = new List<string>();
            if (!Directory.Exists(dir))
            {
                return result;
            }

            foreach (string name in LibVlcNames)
            {
                string p = Path.Combine(dir, name);
                if (File.Exists(p))
                {
                    result.Add(p);
                }
            }

            try
            {
                foreach (string p in Directory.GetFiles(dir, GlobPattern))
                {
                    if (Path.GetFileName(p).StartsWith("libvlc.", StringComparison.Ordinal) && !result.Contains(p))
                    {
                        result.Add(p);
                    }
                }
            }
            catch
            {
                // ignore directory enumeration faults
            }

            result.Sort((a, b) => Path.GetFileName(b).Length.CompareTo(Path.GetFileName(a).Length));
            return result;
        }

        private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (!string.Equals(libraryName, "libvlc", StringComparison.Ordinal))
            {
                return IntPtr.Zero; // defer other libraries (e.g. libc) to the default resolver
            }

            var errors = new List<string>();
            foreach (string path in CandidatePaths())
            {
                try
                {
                    return NativeLibrary.Load(path);
                }
                catch (Exception ex)
                {
                    errors.Add($"{path}: {ex.Message}");
                }
            }

            if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out IntPtr handle))
            {
                return handle;
            }

            string baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
            Console.Error.WriteLine($"[VLCDotNet] could not load native libvlc. baseDir={baseDir}");
            foreach (string e in errors)
            {
                Console.Error.WriteLine("  load-error: " + e);
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
