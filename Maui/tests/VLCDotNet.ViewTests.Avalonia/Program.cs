using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Headless;
using VLCDotNet.ViewTests;

namespace VLCDotNet.ViewTests.Avalonia
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Contains("--autorun"))
            {
                return RunHeadless(args);
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        private static AppBuilder BuildHeadlessApp() =>
            AppBuilder.Configure<App>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                .WithInterFont();

        /// <summary>CI entry point: returns the number of failed tests as the exit code.</summary>
        private static int RunHeadless(string[] args)
        {
            TestEnvironment env = DesktopTestHost.CreateEnvironment(o => Console.WriteLine(o.ToString()));
            Console.WriteLine("Media:  " + env.MediaDirectory);
            Console.WriteLine("Output: " + env.OutputDirectory);

            // 1. Shared high-level suite (pure Core, no Avalonia) - always runs.
            var results = new List<TestOutcome>(new HighLevelTestSuite(env).Run());

            // 2. Avalonia VideoView render test (headless Avalonia) - guarded so a
            //    headless-init problem cannot fail the whole run.
            TestOutcome viewOutcome;
            try
            {
                viewOutcome = RunAvaloniaViewTest(env, args);
            }
            catch (Exception ex)
            {
                viewOutcome = new TestOutcome("View", "Avalonia VideoView render")
                {
                    Passed = false,
                    Message = "headless init failed: " + ex.Message,
                };
            }

            Console.WriteLine(viewOutcome.ToString());
            results.Add(viewOutcome);

            int passed = results.Count(r => r.Passed);
            int failures = results.Count(r => !r.Passed && !r.Skipped);
            int skipped = results.Count(r => r.Skipped);
            Console.WriteLine($"{Environment.NewLine}== {passed} passed, {failures} failed, {skipped} skipped ==");

            WriteDoneMarker(env.OutputDirectory, failures, $"{passed} passed, {failures} failed, {skipped} skipped");
            return failures;
        }

        private static TestOutcome RunAvaloniaViewTest(TestEnvironment env, string[] args)
        {
            TestOutcome? outcome = null;
            BuildHeadlessApp().Start((app, a) => outcome = AvaloniaViewTest.Run(env), args);
            return outcome ?? new TestOutcome("View", "Avalonia VideoView render")
            {
                Passed = false,
                Message = "headless app did not run the view test",
            };
        }

        private static void WriteDoneMarker(string outputDir, int failures, string summary)
        {
            try
            {
                File.WriteAllText(Path.Combine(outputDir, "DONE.txt"), failures + "\n" + summary + "\n");
            }
            catch
            {
                // best effort
            }
        }
    }
}
