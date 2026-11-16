using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Threading;
using VLCDotNet;
using VLCDotNet.Avalonia;

namespace VLCDotNet.ViewTests.Avalonia
{
    /// <summary>
    /// Headless integration test for the Avalonia <see cref="VideoView"/>: hosts
    /// the control in a headless window, plays a real clip through it in memory
    /// mode, pumps the dispatcher so the view's render path runs, and verifies
    /// the view produced visible video frames (with a BMP snapshot).
    /// </summary>
    public static class AvaloniaViewTest
    {
        public static TestOutcome Run(TestEnvironment env)
        {
            var o = new TestOutcome("View", "Avalonia VideoView render");
            var sw = Stopwatch.StartNew();
            string path = Path.Combine(env.MediaDirectory, MediaManifest.Videos[0].FileName);
            if (!File.Exists(path))
            {
                o.Skipped = true;
                o.Message = "skipped: media not found";
                o.Duration = sw.Elapsed;
                return o;
            }

            LibVLC? libvlc = null;
            MediaPlayer? player = null;
            Window? window = null;
            try
            {
                libvlc = new LibVLC("--no-video-title-show", "--no-avcodec-dr", "--verbose=1");
                var media = new Media(libvlc, path, FromType.FromPath);
                player = new MediaPlayer(media);

                var view = new VideoView
                {
                    MediaPlayer = player,
                    RenderMode = VideoViewRenderMode.Memory,
                    MemoryRenderWidth = 320,
                    MemoryRenderHeight = 180,
                };

                window = new Window { Width = 360, Height = 220, Content = view };
                window.Show();
                Dispatcher.UIThread.RunJobs();

                player.Play();

                var timer = Stopwatch.StartNew();
                while (timer.ElapsedMilliseconds < 15000 && (player.VideoSource?.FrameCount ?? 0) < 10)
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(100);
                }

                // Pump a little more so the view's RenderFrame runs for recent frames.
                for (int i = 0; i < 12; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(50);
                }

                MemoryVideoSource? source = player.VideoSource;
                VideoFrameStats stats = source?.Analyze() ?? default;
                o.Details.Add($"effectiveMode={view.EffectiveRenderMode}; {stats}");

                string bmp = Path.Combine(env.OutputDirectory, "avalonia-view.bmp");
                if (source?.WriteBmp(bmp) == true)
                {
                    o.Artifacts.Add(bmp);
                }

                player.Stop();

                bool ok = source is not null && stats.LooksLikeVideo;
                o.Passed = ok;
                o.Message = ok
                    ? $"Avalonia VideoView rendered {stats.FrameCount} frames"
                    : "Avalonia VideoView produced no visible frames";
            }
            catch (Exception ex)
            {
                o.Passed = false;
                o.Message = "exception: " + ex;
            }
            finally
            {
                try
                {
                    window?.Close();
                }
                catch
                {
                    // ignore
                }

                player?.Dispose();
                libvlc?.Dispose();
                o.Duration = sw.Elapsed;
            }

            return o;
        }
    }
}
