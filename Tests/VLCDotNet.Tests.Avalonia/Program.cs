using System;
using System.Linq;
using Avalonia;
using VLCDotNet.Tests.Shared;

namespace VLCDotNet.Tests.Avalonia
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Contains("--autorun"))
            {
                return RunHeadless();
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        /// <summary>Headless entry point used by CI; returns the number of failed tests as the exit code.</summary>
        private static int RunHeadless()
        {
            TestEnvironment env = DesktopTestHost.CreateEnvironment(o => Console.WriteLine(o.ToString()));
            Console.WriteLine("Media:  " + env.MediaDirectory);
            Console.WriteLine("Output: " + env.OutputDirectory);

            var results = new VlcTestSuite(env).Run();
            int failed = results.Count(r => !r.Passed && !r.Skipped);
            int passed = results.Count(r => r.Passed);
            int skipped = results.Count(r => r.Skipped);
            Console.WriteLine($"{Environment.NewLine}== {passed} passed, {failed} failed, {skipped} skipped ==");
            return failed;
        }
    }
}
