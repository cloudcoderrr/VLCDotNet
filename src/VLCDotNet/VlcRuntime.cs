using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    /// <summary>
    /// Runtime configuration helpers for locating the native VLC plugins that
    /// ship next to the application when using the <c>VLCDotNet3</c> package.
    /// Call <see cref="Configure"/> once before <see cref="LibVlc.libvlc_new"/>.
    /// </summary>
    public static class VlcRuntime
    {
        /// <summary>The resolved plugin directory, if one was found.</summary>
        public static string? PluginPath { get; private set; }

        /// <summary>The resolved native library directory (where libvlc lives).</summary>
        public static string? NativePath { get; private set; }

        /// <summary>
        /// Points libvlc at the bundled plugin directory by setting the
        /// <c>VLC_PLUGIN_PATH</c> environment variable. The .NET SDK copies the
        /// package's <c>runtimes/&lt;rid&gt;/native</c> payload next to the app,
        /// so plugins normally live in <c>&lt;baseDir&gt;/plugins</c>.
        /// </summary>
        /// <param name="baseDirectory">Override for the search root (defaults to <see cref="AppContext.BaseDirectory"/>).</param>
        /// <returns><see langword="true"/> if a plugin directory was located.</returns>
        public static bool Configure(string? baseDirectory = null)
        {
            // iOS statically links libvlc and its plugins; nothing to locate.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS")))
            {
                return false;
            }

            string baseDir = baseDirectory ?? AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
            NativePath = baseDir;

            string candidate = Path.Combine(baseDir, "plugins");
            if (Directory.Exists(candidate))
            {
                PluginPath = candidate;
                Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", candidate);
                return true;
            }

            // Fallback: some layouts keep the plugins under the RID native folder.
            string rid = RuntimeInformation.RuntimeIdentifier;
            string ridCandidate = Path.Combine(baseDir, "runtimes", rid, "native", "plugins");
            if (Directory.Exists(ridCandidate))
            {
                PluginPath = ridCandidate;
                NativePath = Path.Combine(baseDir, "runtimes", rid, "native");
                Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", ridCandidate);
                return true;
            }

            return false;
        }
    }
}
