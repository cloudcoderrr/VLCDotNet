using System.Collections.ObjectModel;
using System.Diagnostics;
using VLCDotNet;
using VLCDotNet.ViewTests;

namespace VLCDotNet.ViewTests.Maui
{
    public partial class MainPage : ContentPage
    {
        private readonly ObservableCollection<TestOutcome> _results = new();
        private LibVLC? _libvlc;
        private MediaPlayer? _demoPlayer;
        private Media? _demoMedia;
        private string _mediaDir = string.Empty;
        private bool _started;

        public MainPage()
        {
            InitializeComponent();
            ResultsView.ItemsSource = _results;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
#if AUTORUN
            if (!_started)
            {
                _started = true;
                Dispatcher.Dispatch(async () => await RunAsync());
            }
#endif
        }

        private async void OnRunClicked(object? sender, EventArgs e) => await RunAsync();

        private async void OnPlay(object? sender, EventArgs e)
        {
            await EnsureEngineAsync();
            _demoPlayer?.Play();
        }

        private void OnPause(object? sender, EventArgs e) => _demoPlayer?.Pause();

        private void OnStop(object? sender, EventArgs e) => _demoPlayer?.Stop();

        private async Task EnsureEngineAsync()
        {
            if (_libvlc is not null)
            {
                return;
            }

            _mediaDir = await MauiTestHost.StageMediaAsync();
            MauiTestHost.ConfigurePluginPath();

            _libvlc = new LibVLC("--no-video-title-show", "--no-avcodec-dr", "--verbose=1");
            _demoPlayer = new MediaPlayer(_libvlc);
            DemoVideo.MediaPlayer = _demoPlayer;

            string first = Path.Combine(_mediaDir, MediaManifest.Videos[0].FileName);
            if (File.Exists(first))
            {
                _demoMedia = new Media(_libvlc, first, FromType.FromPath);
                _demoPlayer.Media = _demoMedia;
            }
        }

        private async Task RunAsync()
        {
            RunButton.IsEnabled = false;
            _results.Clear();
            SummaryLabel.Text = "Preparing media…";

            string output = MauiTestHost.OutputDirectory();
            try
            {
                await EnsureEngineAsync();
                MauiTestHost.DumpNativeLayout(output);

                SummaryLabel.Text = "Running…";
                var env = new TestEnvironment
                {
                    MediaDirectory = _mediaDir,
                    OutputDirectory = output,
                    Progress = o =>
                    {
                        Console.WriteLine($"[vlcdotnet-viewtests] {o}");
                        MainThread.BeginInvokeOnMainThread(() => _results.Add(o));
                    },
                };

                var results = new List<TestOutcome>(await Task.Run(() => new HighLevelTestSuite(env).Run()));

                TestOutcome viewOutcome = await RunViewTestAsync(env);
                _results.Add(viewOutcome);
                results.Add(viewOutcome);
                Console.WriteLine($"[vlcdotnet-viewtests] {viewOutcome}");

                int pass = results.Count(r => r.Passed);
                int fail = results.Count(r => !r.Passed && !r.Skipped);
                int skip = results.Count(r => r.Skipped);
                SummaryLabel.Text = $"{pass} passed, {fail} failed, {skip} skipped — {output}";
                Console.WriteLine($"[vlcdotnet-viewtests] completed pass={pass} fail={fail} skip={skip}");
                WriteMarker(output, $"{pass} passed, {fail} failed, {skip} skipped", fail);
            }
            catch (Exception ex)
            {
                SummaryLabel.Text = "Error: " + ex.Message;
                Console.WriteLine("[vlcdotnet-viewtests] error: " + ex);
                WriteMarker(output, "error: " + ex, 99);
            }
            finally
            {
                RunButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Plays a clip through the on-screen MAUI <c>VideoView</c> and verifies
        /// it produced visible video. On Android the portable bitmap source is
        /// analysed; on Apple the native (VideoToolbox) output is confirmed via a
        /// libvlc snapshot.
        /// </summary>
        private async Task<TestOutcome> RunViewTestAsync(TestEnvironment env)
        {
            var o = new TestOutcome("View", "MAUI VideoView render");
            var sw = Stopwatch.StartNew();
            try
            {
                if (_demoPlayer is null)
                {
                    o.Skipped = true;
                    o.Message = "skipped: engine not available";
                    o.Duration = sw.Elapsed;
                    return o;
                }

                _demoPlayer.Play();

                var timer = Stopwatch.StartNew();
                while (timer.ElapsedMilliseconds < 15000)
                {
                    await Task.Delay(300);
                    long frames = _demoPlayer.VideoSource?.FrameCount ?? 0;
                    if (frames > 10)
                    {
                        break;
                    }

                    if (_demoPlayer.VideoSource is null && _demoPlayer.VideoOutputCount > 0 && timer.ElapsedMilliseconds > 4000)
                    {
                        break;
                    }
                }

                MemoryVideoSource? source = _demoPlayer.VideoSource;
                if (source is not null)
                {
                    VideoFrameStats stats = source.Analyze();
                    string bmp = Path.Combine(env.OutputDirectory, "maui-view.bmp");
                    if (source.WriteBmp(bmp))
                    {
                        o.Artifacts.Add(bmp);
                    }

                    o.Details.Add("memory mode; " + stats);
                    o.Passed = stats.LooksLikeVideo;
                    o.Message = o.Passed ? $"MAUI VideoView rendered {stats.FrameCount} frames" : "MAUI VideoView produced no visible frames";
                }
                else
                {
                    uint vout = _demoPlayer.VideoOutputCount;
                    string png = Path.Combine(env.OutputDirectory, "maui-view.png");
                    bool snap = _demoPlayer.TakeSnapshot(0, png, 0, 0);
                    o.Details.Add($"native mode; vout={vout}, snapshot={snap}");
                    if (snap && File.Exists(png))
                    {
                        o.Artifacts.Add(png);
                    }

                    o.Passed = vout > 0 && snap;
                    o.Message = o.Passed ? "MAUI VideoView active (native output) + snapshot captured" : "native VideoView produced no output";
                }

                _demoPlayer.Stop();
            }
            catch (Exception ex)
            {
                o.Passed = false;
                o.Message = "exception: " + ex;
            }
            finally
            {
                o.Duration = sw.Elapsed;
            }

            return o;
        }

        private static void WriteMarker(string output, string summary, int failures)
        {
            try
            {
                File.WriteAllText(Path.Combine(output, "DONE.txt"), failures + "\n" + summary + "\n");
                Console.WriteLine($"[vlcdotnet-viewtests] wrote DONE.txt failures={failures}");
            }
            catch
            {
                // best effort
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _demoPlayer?.Dispose();
            _demoMedia?.Dispose();
            _libvlc?.Dispose();
            _demoPlayer = null;
            _demoMedia = null;
            _libvlc = null;
        }
    }
}
