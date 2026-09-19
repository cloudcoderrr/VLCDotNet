using System;
using System.Collections.Generic;
using System.IO;
using VLCDotNet.Tests.Shared;

namespace VLCDotNet.Tests.Avalonia
{
    /// <summary>
    /// Resolves the media/output folders and runs the shared suite. Shared by
    /// the GUI and the <c>--autorun</c> headless (CI) entry point.
    /// </summary>
    public static class DesktopTestHost
    {
        public static string ResolveMediaDirectory()
        {
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "vlc_test_pack"),
            };

            // Walk up from the app base to find the repo's Tests/Data pack (dev runs).
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
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
