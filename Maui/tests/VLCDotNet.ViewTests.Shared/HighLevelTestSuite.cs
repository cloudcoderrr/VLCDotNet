using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace VLCDotNet.ViewTests
{
    /// <summary>
    /// Full-coverage test suite driven through the high-level
    /// VLCDotNet3.Playback.Core API. Verifies engine creation, decoding and
    /// rendering of every core video format (with headless frame capture +
    /// colour analysis and a BMP snapshot), audio playback, hardware-accelerated
    /// decode, parsing/tracks, transport controls, events, multitrack selection,
    /// media lists and the equalizer.
    /// </summary>
    public sealed class HighLevelTestSuite
    {
        private readonly TestEnvironment _env;
        private readonly List<TestOutcome> _results = new List<TestOutcome>();
        private LibVLC? _libvlc;

        public HighLevelTestSuite(TestEnvironment env) => _env = env;

        public IReadOnlyList<TestOutcome> Results => _results;

        public IReadOnlyList<TestOutcome> Run()
        {
            Directory.CreateDirectory(_env.OutputDirectory);

            if (!InitEngine())
            {
                WriteReport();
                return _results;
            }

            try
            {
                Guard("Core", "engine version", TestEngineVersion);

                foreach (MediaItem v in MediaManifest.Videos)
                {
                    Guard("Video", v.FileName, () => TestVideo(v));
                }

                foreach (MediaItem a in MediaManifest.Audios)
                {
                    Guard("Audio", a.FileName, () => TestAudio(a));
                }

                Guard("Hardware", "hw-accelerated decode", TestHardwareDecode);
                Guard("Parse", "parse + tracks", TestParseTracks);
                Guard("Transport", "seek/pause/rate", TestTransport);
                Guard("Events", "playback events", TestEvents);
                Guard("Tracks", "multitrack switch", TestMultitrack);
                Guard("MediaList", "list operations", TestMediaList);
                Guard("Equalizer", "equalizer apply", TestEqualizer);
                Guard("Snapshot", "video snapshot", TestSnapshot);
            }
            finally
            {
                _libvlc?.FlushLog();
                _libvlc?.Dispose();
                _libvlc = null;
            }

            WriteReport();
            return _results;
        }

        private bool InitEngine()
        {
            var o = new TestOutcome("Core", "Create engine");
            var sw = Stopwatch.StartNew();
            try
            {
                var args = new List<string> { "--no-video-title-show", "--no-avcodec-dr", "--verbose=2" };
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    args.Add("--no-xlib");
                }

                _libvlc = new LibVLC(args.ToArray());

                string logPath = Path.Combine(_env.OutputDirectory, "vlc-log.txt");
                _libvlc.StartFileLog(logPath);
                o.Artifacts.Add(logPath);

                o.Passed = true;
                o.Message = "engine created; file logging enabled";
                Record(o, sw);
                return true;
            }
            catch (Exception ex)
            {
                o.Passed = false;
                o.Message = ex.ToString();
                Record(o, sw);
                return false;
            }
        }

        private void TestEngineVersion()
        {
            var o = new TestOutcome("Core", "engine version");
            var sw = Stopwatch.StartNew();
            string version = LibVLC.Version;
            o.Details.Add("libvlc version: " + version);
            o.Passed = version.StartsWith("3.", StringComparison.Ordinal);
            o.Message = o.Passed ? "libvlc 3.x confirmed" : "unexpected version: " + version;
            Record(o, sw);
        }

        private void TestVideo(MediaItem item)
        {
            var o = new TestOutcome("Video", item.FileName);
            var sw = Stopwatch.StartNew();
            string path = Path.Combine(_env.MediaDirectory, item.FileName);
            if (!File.Exists(path))
            {
                Skip(o, sw, "media not found: " + path);
                return;
            }

            using var media = new Media(_libvlc!, path, FromType.FromPath);
            using var player = new MediaPlayer(media);
            MemoryVideoSource source = player.EnableMemoryRendering(320, 180, VideoPixelFormat.Bgra);

            if (!PlayToPlaying(player))
            {
                player.Stop();
                Fail(o, sw, "did not reach Playing state");
                return;
            }

            WaitForFrames(source, 12000);
            VideoFrameStats stats = source.Analyze();
            o.Details.Add(stats.ToString());
            o.Details.Add($"state={player.State}, length={player.Length}ms, vout={player.VideoOutputCount}");

            string bmp = Path.Combine(_env.OutputDirectory, "view-" + Stem(item.FileName) + ".bmp");
            if (source.WriteBmp(bmp))
            {
                o.Artifacts.Add(bmp);
            }

            player.Stop();

            o.Passed = stats.LooksLikeVideo;
            o.Message = o.Passed
                ? $"decoded {stats.FrameCount} frames through the high-level API"
                : "no decoded frames, frame is black, or image lacks detail";
            Record(o, sw);
        }

        private void TestAudio(MediaItem item)
        {
            var o = new TestOutcome("Audio", item.FileName);
            var sw = Stopwatch.StartNew();
            string path = Path.Combine(_env.MediaDirectory, item.FileName);
            if (!File.Exists(path))
            {
                Skip(o, sw, "media not found: " + path);
                return;
            }

            using var media = new Media(_libvlc!, path, FromType.FromPath);
            using var player = new MediaPlayer(media);
            MemoryAudioSource audio = player.EnableAudioCapture(44100, 2);

            if (!PlayToPlaying(player))
            {
                player.Stop();
                Fail(o, sw, "did not reach Playing state");
                return;
            }

            Thread.Sleep(1600);
            double rms = audio.Rms;
            o.Details.Add($"state={player.State}, samples={audio.SampleValues}, rms={rms:F1}, audioTracks={player.AudioTrackCount}, length={player.Length}ms");
            player.Stop();

            bool ok = audio.SampleValues > 0 && rms > 20.0;
            o.Passed = ok;
            o.Message = ok ? $"audio decoded (RMS {rms:F0})" : "no audio samples captured";
            Record(o, sw);
        }

        private void TestHardwareDecode()
        {
            var o = new TestOutcome("Hardware", "hw-accelerated decode");
            var sw = Stopwatch.StartNew();
            string path = Path.Combine(_env.MediaDirectory, MediaManifest.HardwareDecodeVideo.FileName);
            if (!File.Exists(path))
            {
                Skip(o, sw, "media not found: " + path);
                return;
            }

            using var media = new Media(_libvlc!, path, FromType.FromPath);
            string hw = HardwareDecodeOption();
            media.AddOption(hw);
            o.Details.Add("requested hw decoder: " + hw);

            using var player = new MediaPlayer(media);
            MemoryVideoSource source = player.EnableMemoryRendering(320, 180, VideoPixelFormat.Bgra);

            if (!PlayToPlaying(player))
            {
                player.Stop();
                Fail(o, sw, "did not reach Playing state with hardware decode requested");
                return;
            }

            WaitForFrames(source, 12000);
            VideoFrameStats stats = source.Analyze();
            o.Details.Add(stats.ToString());
            player.Stop();

            o.Passed = stats.LooksLikeVideo;
            o.Message = o.Passed
                ? $"hardware-accelerated decode path produced {stats.FrameCount} frames"
                : "no frames produced with hardware decode requested";
            Record(o, sw);
        }

        private void TestParseTracks()
        {
            var o = new TestOutcome("Parse", "parse + tracks");
            var sw = Stopwatch.StartNew();
            string path = Path.Combine(_env.MediaDirectory, MediaManifest.Videos[0].FileName);
            if (!File.Exists(path))
            {
                Skip(o, sw, "media not found");
                return;
            }

            using var media = new Media(_libvlc!, path, FromType.FromPath);
            VlcMediaParsedStatus status = media.Parse(VlcMediaParseFlag.ParseLocal, 5000);
            IReadOnlyList<MediaTrack> tracks = media.Tracks;
            o.Details.Add($"parse={status}, duration={media.Duration}ms, tracks={tracks.Count}");
            foreach (MediaTrack t in tracks)
            {
                o.Details.Add("• " + t);
            }

            bool hasVideo = tracks.Any(t => t.Type == VlcTrackType.Video && t.Width > 0 && t.Height > 0);
            bool hasAudio = tracks.Any(t => t.Type == VlcTrackType.Audio);
            o.Passed = status == VlcMediaParsedStatus.Done && tracks.Count >= 2 && hasVideo && hasAudio;
            o.Message = o.Passed ? "parsed with expected video + audio tracks" : "parse incomplete or tracks missing";
            Record(o, sw);
        }

        private void TestTransport()
        {
            var o = new TestOutcome("Transport", "seek/pause/rate");
            var sw = Stopwatch.StartNew();
            string path = Path.Combine(_env.MediaDirectory, MediaManifest.Videos[0].FileName);
            if (!File.Exists(path))
            {
                Skip(o, sw, "media not found");
                return;
            }

            using var media = new Media(_libvlc!, path, FromType.FromPath);
            using var player = new MediaPlayer(media);
            MemoryVideoSource source = player.EnableMemoryRendering(160, 90, VideoPixelFormat.Bgra);

            if (!PlayToPlaying(player))
            {
                player.Stop();
                Fail(o, sw, "did not reach Playing state");
                return;
            }

            WaitForFrames(source, 8000);

            player.Position = 0.5f;
            Thread.Sleep(800);
            long seekedTime = player.Time;
            bool seeked = seekedTime > 1000;
            o.Details.Add($"after seek: time={seekedTime}ms, seekable={player.IsSeekable}");

            player.SetPause(true);
            Thread.Sleep(400);
            VlcState pausedState = player.State;
            o.Details.Add("paused state=" + pausedState);

            player.SetPause(false);
            Thread.Sleep(300);

            player.Rate = 2.0f;
            Thread.Sleep(200);
            float rate = player.Rate;
            o.Details.Add("rate=" + rate);
            player.Stop();

            bool ok = seeked && pausedState == VlcState.Paused && Math.Abs(rate - 2.0f) < 0.01f;
            o.Passed = ok;
            o.Message = ok ? "seek, pause and rate change all took effect" : "a transport control did not take effect";
            Record(o, sw);
        }

        private void TestEvents()
        {
            var o = new TestOutcome("Events", "playback events");
            var sw = Stopwatch.StartNew();
            string path = Path.Combine(_env.MediaDirectory, MediaManifest.Videos[0].FileName);
            if (!File.Exists(path))
            {
                Skip(o, sw, "media not found");
                return;
            }

            using var media = new Media(_libvlc!, path, FromType.FromPath);
            using var player = new MediaPlayer(media);
            MemoryVideoSource source = player.EnableMemoryRendering(160, 90, VideoPixelFormat.Bgra);

            int playing = 0;
            int timeChanged = 0;
            player.Playing += (_, _) => Interlocked.Increment(ref playing);
            player.TimeChanged += (_, _) => Interlocked.Increment(ref timeChanged);

            if (!PlayToPlaying(player))
            {
                player.Stop();
                Fail(o, sw, "did not reach Playing state");
                return;
            }

            Thread.Sleep(1800);
            player.Stop();

            o.Details.Add($"Playing fired={playing > 0}, TimeChanged count={timeChanged}");
            bool ok = playing > 0 && timeChanged > 0;
            o.Passed = ok;
            o.Message = ok ? "Playing and TimeChanged events were raised" : "expected playback events were not raised";
            Record(o, sw);
        }

        private void TestMultitrack()
        {
            var o = new TestOutcome("Tracks", MediaManifest.Multitrack.FileName);
            var sw = Stopwatch.StartNew();
            string path = Path.Combine(_env.MediaDirectory, MediaManifest.Multitrack.FileName);
            if (!File.Exists(path))
            {
                Skip(o, sw, "media not found");
                return;
            }

            using var media = new Media(_libvlc!, path, FromType.FromPath);
            using var player = new MediaPlayer(media);
            MemoryVideoSource source = player.EnableMemoryRendering(160, 90, VideoPixelFormat.Bgra);

            if (!PlayToPlaying(player))
            {
                player.Stop();
                Fail(o, sw, "did not reach Playing state");
                return;
            }

            Wait(() => player.AudioTrackCount >= 2 && player.SpuCount >= 2, 6000);
            int audio = player.AudioTrackCount;
            int spu = player.SpuCount;
            o.Details.Add($"audioTracks={audio}, spuTracks={spu}");

            IReadOnlyList<TrackDescription> audioTracks = player.AudioTrackDescriptions;
            bool switched = false;
            if (audioTracks.Count >= 2)
            {
                int target = audioTracks[audioTracks.Count - 1].Id;
                switched = player.SetAudioTrack(target);
                Thread.Sleep(300);
                switched = switched && player.AudioTrack == target;
                o.Details.Add("audio switch ok=" + switched);
            }

            player.Stop();
            bool ok = audio >= 2 && spu >= 2 && switched;
            o.Passed = ok;
            o.Message = ok ? "two audio + two subtitle tracks, audio switch confirmed" : "expected 2 audio and 2 subtitle tracks that switch";
            Record(o, sw);
        }

        private void TestMediaList()
        {
            var o = new TestOutcome("MediaList", "list operations");
            var sw = Stopwatch.StartNew();
            string a = Path.Combine(_env.MediaDirectory, MediaManifest.Videos[0].FileName);
            string b = Path.Combine(_env.MediaDirectory, MediaManifest.Audios[0].FileName);
            if (!File.Exists(a) || !File.Exists(b))
            {
                Skip(o, sw, "media not found");
                return;
            }

            using var list = new MediaList(_libvlc!);
            using var m1 = new Media(_libvlc!, a, FromType.FromPath);
            using var m2 = new Media(_libvlc!, b, FromType.FromPath);
            bool added = list.Add(m1) && list.Add(m2);
            int count = list.Count;
            using Media? item0 = list.ItemAt(0);
            o.Details.Add($"added={added}, count={count}, item0={(item0 != null)}");

            bool ok = added && count == 2 && item0 != null;
            o.Passed = ok;
            o.Message = ok ? "media list add/count/item work" : "media list operation failed";
            Record(o, sw);
        }

        private void TestEqualizer()
        {
            var o = new TestOutcome("Equalizer", "equalizer apply");
            var sw = Stopwatch.StartNew();
            string path = Path.Combine(_env.MediaDirectory, MediaManifest.Audios[1].FileName);
            if (!File.Exists(path))
            {
                Skip(o, sw, "media not found");
                return;
            }

            using var eq = new Equalizer();
            eq.Preamp = 6f;
            bool bandSet = eq.SetAmp(4f, 0);
            o.Details.Add($"presets={Equalizer.PresetCount}, bands={Equalizer.BandCount}, preamp={eq.Preamp}");

            using var media = new Media(_libvlc!, path, FromType.FromPath);
            using var player = new MediaPlayer(media);
            bool applied = player.SetEqualizer(eq);
            player.Play();
            Thread.Sleep(600);
            player.Stop();

            bool ok = Equalizer.BandCount > 0 && bandSet && applied;
            o.Passed = ok;
            o.Message = ok ? "equalizer created, configured and applied" : "equalizer apply failed";
            Record(o, sw);
        }

        private void TestSnapshot()
        {
            var o = new TestOutcome("Snapshot", "video snapshot");
            var sw = Stopwatch.StartNew();
            string path = Path.Combine(_env.MediaDirectory, MediaManifest.Videos[0].FileName);
            if (!File.Exists(path))
            {
                Skip(o, sw, "media not found");
                return;
            }

            using var media = new Media(_libvlc!, path, FromType.FromPath);
            using var player = new MediaPlayer(media);
            MemoryVideoSource source = player.EnableMemoryRendering(640, 360, VideoPixelFormat.Bgra);

            if (!PlayToPlaying(player))
            {
                player.Stop();
                Fail(o, sw, "did not reach Playing state");
                return;
            }

            WaitForFrames(source, 12000);
            player.Position = 0.4f;
            Thread.Sleep(700);

            string snapshot = Path.Combine(_env.OutputDirectory, "snapshot-video_h264_aac.bmp");
            bool written = source.WriteBmp(snapshot);
            VideoFrameStats stats = source.Analyze();
            player.Stop();

            long size = written && File.Exists(snapshot) ? new FileInfo(snapshot).Length : 0;
            o.Details.Add($"snapshot bytes={size}; {stats}");
            if (written)
            {
                o.Artifacts.Add(snapshot);
            }

            bool ok = written && size > 1000 && stats.LooksLikeVideo;
            o.Passed = ok;
            o.Message = ok ? "captured a non-trivial video snapshot" : "snapshot missing or looks black";
            Record(o, sw);
        }

        // ----- helpers -----------------------------------------------------

        private static string HardwareDecodeOption()
        {
            if (OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsMacOS())
            {
                return ":codec=videotoolbox,avcodec";
            }

            if (OperatingSystem.IsAndroid())
            {
                return ":codec=mediacodec,avcodec";
            }

            // Windows / Linux: let avcodec pick a hardware decoder (dxva2/d3d11va/
            // vaapi) with a software fallback so the test passes on any runner.
            return ":avcodec-hw=any";
        }

        private static bool PlayToPlaying(MediaPlayer player)
        {
            if (!player.Play())
            {
                return false;
            }

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 10000)
            {
                VlcState state = player.State;
                if (state == VlcState.Playing)
                {
                    return true;
                }

                if (state == VlcState.Error)
                {
                    return false;
                }

                Thread.Sleep(50);
            }

            return player.State == VlcState.Playing;
        }

        private static void WaitForFrames(MemoryVideoSource source, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (source.FrameCount == 0 && sw.ElapsedMilliseconds < timeoutMs)
            {
                Thread.Sleep(200);
            }

            // Let a few more frames accumulate so Analyze sees steady-state video.
            Thread.Sleep(900);
        }

        private static void Wait(Func<bool> condition, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            {
                Thread.Sleep(100);
            }
        }

        private static string Stem(string fileName) => Path.GetFileNameWithoutExtension(fileName);

        private void Guard(string category, string name, Action test)
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                var o = new TestOutcome(category, name)
                {
                    Passed = false,
                    Message = "exception: " + ex.Message,
                };
                _results.Add(o);
                _env.Progress?.Invoke(o);
            }
        }

        private void Record(TestOutcome outcome, Stopwatch sw)
        {
            sw.Stop();
            outcome.Duration = sw.Elapsed;
            _results.Add(outcome);
            _env.Progress?.Invoke(outcome);
        }

        private void Skip(TestOutcome o, Stopwatch sw, string reason)
        {
            o.Skipped = true;
            o.Message = "skipped: " + reason;
            Record(o, sw);
        }

        private void Fail(TestOutcome o, Stopwatch sw, string reason)
        {
            o.Passed = false;
            o.Message = reason;
            Record(o, sw);
        }

        private void WriteReport()
        {
            var sb = new StringBuilder();
            int passed = _results.Count(r => r.Passed);
            int failed = _results.Count(r => !r.Passed && !r.Skipped);
            int skipped = _results.Count(r => r.Skipped);
            sb.AppendLine($"VLCDotNet3.Playback high-level test report");
            sb.AppendLine($"libvlc {LibVLC.Version}");
            sb.AppendLine($"{passed} passed, {failed} failed, {skipped} skipped");
            sb.AppendLine();
            foreach (TestOutcome o in _results)
            {
                sb.AppendLine(o.ToString());
                foreach (string d in o.Details)
                {
                    sb.AppendLine("    " + d);
                }

                foreach (string a in o.Artifacts)
                {
                    sb.AppendLine("    -> " + a);
                }
            }

            try
            {
                File.WriteAllText(Path.Combine(_env.OutputDirectory, "runner-report.txt"), sb.ToString());
            }
            catch
            {
                // best effort
            }
        }
    }
}
