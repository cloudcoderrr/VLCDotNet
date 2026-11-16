using System.Text;
using VLCDotNet;

namespace VLCDotNet.ViewTests.Maui
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

            foreach (string name in MediaManifest.AllFileNames())
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
            string? overrideDir = Environment.GetEnvironmentVariable("VLCDOTNET_TEST_OUTPUT");
            string dir = string.IsNullOrWhiteSpace(overrideDir)
                ? Path.Combine(FileSystem.AppDataDirectory, "test-output")
                : overrideDir;
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

        public static void DumpNativeLayout(string outputDirectory)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"AppContext.BaseDirectory={AppContext.BaseDirectory}");
            sb.AppendLine($"VLC_PLUGIN_PATH={Environment.GetEnvironmentVariable("VLC_PLUGIN_PATH")}");
            sb.AppendLine($"VlcRuntime.PluginPath={VlcRuntime.PluginPath}");
            sb.AppendLine($"VlcRuntime.NativePath={VlcRuntime.NativePath}");
            sb.AppendLine($"libvlc version={LibVLC.Version}");

#if ANDROID
            var info = Android.App.Application.Context.ApplicationInfo;
            sb.AppendLine($"NativeLibraryDir={info?.NativeLibraryDir}");
            if (!string.IsNullOrEmpty(info?.NativeLibraryDir) && Directory.Exists(info.NativeLibraryDir))
            {
                foreach (string file in Directory.EnumerateFiles(info.NativeLibraryDir).OrderBy(Path.GetFileName))
                {
                    sb.AppendLine(Path.GetFileName(file));
                }
            }
#endif

            try
            {
                File.WriteAllText(Path.Combine(outputDirectory, "native-layout.txt"), sb.ToString());
            }
            catch
            {
                // best effort
            }
        }
    }
}
