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

            // The Linux build links the contrib fontconfig, whose baked default
            // config path points at the (absent) build-time contrib prefix.
            EnsureLinuxFontconfig();

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

        /// <summary>
        /// Ensures the Linux fontconfig backend used by libvlc's text renderer has
        /// a readable configuration. The from-source Linux build statically links
        /// the contrib fontconfig, whose compiled-in default config path is the
        /// build machine's contrib prefix, which does not exist on the target. With
        /// no config, fontconfig logs "Cannot load default config file" and the
        /// freetype renderer can dereference the resulting NULL config and crash.
        /// If the host application has not configured fontconfig itself, write a
        /// minimal, version-agnostic config that points at the usual system font
        /// directories with a writable cache, so font lookup degrades gracefully
        /// instead of crashing. No-op on non-Linux platforms (Windows uses
        /// DirectWrite and Apple uses CoreText, so fontconfig is not linked).
        /// </summary>
        private static void EnsureLinuxFontconfig()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return;
            }

            // Respect any fontconfig setup the host has already provided.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FONTCONFIG_PATH")) ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FONTCONFIG_FILE")))
            {
                return;
            }

            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "vlcdotnet-fontconfig");
                string cache = Path.Combine(dir, "cache");
                Directory.CreateDirectory(cache);

                string conf = Path.Combine(dir, "fonts.conf");
                if (!File.Exists(conf))
                {
                    File.WriteAllText(
                        conf,
                        "<?xml version=\"1.0\"?>\n" +
                        "<!DOCTYPE fontconfig SYSTEM \"fonts.dtd\">\n" +
                        "<fontconfig>\n" +
                        "  <dir>/usr/share/fonts</dir>\n" +
                        "  <dir>/usr/local/share/fonts</dir>\n" +
                        "  <dir>~/.fonts</dir>\n" +
                        "  <cachedir>" + cache + "</cachedir>\n" +
                        "</fontconfig>\n");
                }

                Environment.SetEnvironmentVariable("FONTCONFIG_PATH", dir);
            }
            catch
            {
                // Best effort only: if we cannot write the config, leave the
                // environment untouched.
            }
        }
    }
}
