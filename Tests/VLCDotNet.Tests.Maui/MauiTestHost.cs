using System.Collections.Generic;
using VLCDotNet;
using VLCDotNet.Tests.Shared;

namespace VLCDotNet.Tests.Maui
{
    /// <summary>
    /// Stages the bundled media pack into writable app storage (libvlc needs a
    /// real filesystem path) and configures the native plugin path per platform.
    /// </summary>
    public static class MauiTestHost
    {
        public static async Task<string> StageMediaAsync()
        {
            string dir = Path.Combine(FileSystem.AppDataDirectory, "vlc_test_pack");
            Directory.CreateDirectory(dir);

            foreach (string name in AllMediaNames())
            {
                string dest = Path.Combine(dir, name);
                try
                {
                    using Stream src = await FileSystem.OpenAppPackageFileAsync($"vlc_test_pack/{name}");
                    using FileStream fs = File.Create(dest);
                    await src.CopyToAsync(fs);
                }
                catch
                {
                    // A missing file simply causes that test to be skipped.
                }
            }

            return dir;
        }

        public static string OutputDirectory()
        {
            string dir = Path.Combine(FileSystem.AppDataDirectory, "test-output");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void ConfigurePluginPath()
        {
#if ANDROID
            // VLC plugins ship as .so files extracted into the native library dir.
            var info = Android.App.Application.Context.ApplicationInfo;
            if (!string.IsNullOrEmpty(info?.NativeLibraryDir))
            {
                Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", info!.NativeLibraryDir);
            }
#endif
            VlcRuntime.Configure();
        }

        private static IEnumerable<string> AllMediaNames()
        {
            foreach (MediaSpec m in MediaCatalog.Videos)
            {
                yield return m.FileName;
            }

            yield return MediaCatalog.Multitrack.FileName;

            foreach (MediaSpec m in MediaCatalog.Audios)
            {
                yield return m.FileName;
            }

            foreach (string s in MediaCatalog.ExternalSubtitles)
            {
                yield return s;
            }
        }
    }
}
