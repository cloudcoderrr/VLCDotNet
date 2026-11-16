using System;
using System.Collections.Generic;
using System.IO;

namespace VLCDotNet.ViewTests.Avalonia
{
    /// <summary>Resolves the media/output folders for the Avalonia test app.</summary>
    public static class DesktopTestHost
    {
        public static string ResolveMediaDirectory()
        {
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "vlc_test_pack"),
            };

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 9 && dir != null; i++, dir = dir.Parent)
            {
                candidates.Add(Path.Combine(dir.FullName, "Tests", "Data", "vlc_test_pack"));
            }

            foreach (string c in candidates)
            {
                if (Directory.Exists(c))
                {
                    return c;
                }
            }

            return candidates[0];
        }

        public static string ResolveOutputDirectory()
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "test-output");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static TestEnvironment CreateEnvironment(Action<TestOutcome>? progress = null) => new TestEnvironment
        {
            MediaDirectory = ResolveMediaDirectory(),
            OutputDirectory = ResolveOutputDirectory(),
            Progress = progress,
        };
    }
}
