using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace VLCDotNet.Tests.Shared
{
    /// <summary>Inputs for a test run: where the media lives and where artifacts go.</summary>
    public sealed class TestEnvironment
    {
        public string MediaDirectory { get; set; } = string.Empty;

        public string OutputDirectory { get; set; } = string.Empty;

        /// <summary>Optional progress sink (per completed test).</summary>
        public Action<TestOutcome>? Progress { get; set; }
    }

    /// <summary>
    /// Full-coverage libvlc test suite driven purely through the P/Invoke
    /// bindings. Verifies instance/version, per-file parsing and track metadata,
    /// video output (headless frame capture + colour analysis), audio output
    /// (PCM capture + RMS), subtitle/track selection and transport controls, and
    /// that libvlc logs are written to a file.
    /// </summary>
    public sealed class VlcTestSuite
    {
        private static readonly string[] RequiredModules =
        {
            "access_output_file",
            "avcodec",
            "faad",
            "flac",
            "mad",
            "mpc",
            "schroedinger",
            "sid",
            "stream_out_standard",
            "stream_out_transcode",
            "theora",
            "vpx",
        };

        private static readonly string[] AppleMobileRequiredModules =
        {
            "access_output_file",
            "avcodec",
            "stream_out_standard",
            "stream_out_transcode",
            "videotoolbox",
        };

        private readonly TestEnvironment _env;
        private readonly List<TestOutcome> _results = new List<TestOutcome>();
        private IntPtr _instance;
        private VlcFileLog? _log;
        private string _logPath = string.Empty;

        public VlcTestSuite(TestEnvironment env)
        {
            _env = env;
        }

        public IReadOnlyList<TestOutcome> Results => _results;

        public IReadOnlyList<TestOutcome> Run()
        {
            Directory.CreateDirectory(_env.OutputDirectory);

            if (!InitializeInstance())
            {
                WriteReport();
                return _results;
            }

            try
            {
                TestInstanceAndVersion();
                TestRequestedModulesAvailable();

                foreach (MediaSpec spec in MediaCatalog.Videos)
                {
                    TestVideoFile(spec);
                }

                foreach (MediaSpec spec in MediaCatalog.Audios)
                {
                    TestAudioFile(spec);
                }

                TestParse(MediaCatalog.Videos[0]);
                TestParse(MediaCatalog.TransportStream);
                TestMultitrackAudioAndSubtitles();
                TestExternalSubtitles();
                TestTransportControls();
                TestStreamOutputTranscode();
            }
            finally
            {
                _log?.Flush();
                TestLogFileWritten();
                _log?.Dispose();
                if (_instance != IntPtr.Zero)
                {
                    LibVlc.libvlc_release(_instance);
                    _instance = IntPtr.Zero;
                }
            }

            WriteReport();
            return _results;
        }

        // ----- Setup -------------------------------------------------------

        private bool InitializeInstance()
        {
            var outcome = new TestOutcome("Core", "Create libvlc instance");
            var sw = Stopwatch.StartNew();
            try
            {
                VlcRuntime.Configure();
                if (VlcRuntime.PluginPath != null)
                {
                    outcome.Details.Add($"plugin path: {VlcRuntime.PluginPath}");
                }

                var args = new List<string>
                {
                    "--intf=dummy",
                    "--no-video-title-show",
                    // Headless frame capture uses a single shared vmem buffer. With
                    // avcodec direct rendering the frame-threaded decoder pulls
                    // multiple in-flight pictures through that one buffer, which
                    // races (observed as intermittent 'thread_get_buffer() failed'
                    // and a SIGSEGV on Linux). Disabling DR makes the decoder use
                    // its own buffer pool and lets the vout copy frames serially.
                    "--no-avcodec-dr",
                    "--verbose=2",
                };
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    args.Add("--no-xlib");
                }

                _instance = LibVlc.libvlc_new(args.Count, args.ToArray());
                if (_instance == IntPtr.Zero)
                {
                    outcome.Passed = false;
                    outcome.Message = "libvlc_new returned NULL (native libvlc or plugins missing)";
                    Record(outcome, sw);
                    return false;
                }

                _logPath = Path.Combine(_env.OutputDirectory, "vlc-log.txt");
                _log = VlcFileLog.Start(_instance, _logPath);
                outcome.Artifacts.Add(_logPath);

                outcome.Passed = true;
                outcome.Message = "instance created; file logging enabled";
                Record(outcome, sw);
                return true;
            }
            catch (DllNotFoundException ex)
            {
                outcome.Passed = false;
                outcome.Message = "native libvlc not found: " + ex.Message;
                Record(outcome, sw);
                return false;
            }
            catch (Exception ex)
            {
                outcome.Passed = false;
                outcome.Message = ex.ToString();
                Record(outcome, sw);
                return false;
            }
        }

        private void TestInstanceAndVersion()
        {
            var outcome = new TestOutcome("Core", "libvlc version");
            var sw = Stopwatch.StartNew();
            string? version = LibVlc.Utf8ToString(LibVlc.libvlc_get_version());
            outcome.Details.Add("version: " + version);
            outcome.Passed = version != null && version.StartsWith("3.", StringComparison.Ordinal);
            outcome.Message = outcome.Passed ? "libvlc 3.x confirmed" : "unexpected version: " + version;
            Record(outcome, sw);
        }

        private void TestRequestedModulesAvailable()
        {
            var outcome = new TestOutcome("Modules", "requested plugins");
            var sw = Stopwatch.StartNew();
            try
            {
                HashSet<string> modules = DiscoverAvailableModules();
                if (modules.Count == 0)
                {
                    outcome.Passed = false;
                    outcome.Message = "no plugin or static module inventory was discovered";
                    return;
                }

                var missing = new List<string>();
                foreach (string module in GetRequiredModules())
                {
                    if (!modules.Contains(module))
                    {
                        missing.Add(module);
                    }
                }

                var discovered = new List<string>(modules);
                discovered.Sort(StringComparer.OrdinalIgnoreCase);
                outcome.Details.Add("modules: " + string.Join(", ", discovered));

                outcome.Passed = missing.Count == 0;
                outcome.Message = outcome.Passed
                    ? "requested codec and sout modules are present"
                    : "missing modules: " + string.Join(", ", missing);
            }
            catch (Exception ex)
            {
                outcome.Passed = false;
                outcome.Message = ex.Message;
            }
            finally
            {
                Record(outcome, sw);
            }
        }

        // ----- Video -------------------------------------------------------

        private void TestVideoFile(MediaSpec spec)
        {
            var outcome = new TestOutcome("Video", spec.FileName);
            var sw = Stopwatch.StartNew();
            IntPtr media = IntPtr.Zero, mp = IntPtr.Zero;
            VideoFrameCapture? cap = null;
            try
            {
                string path = Path.Combine(_env.MediaDirectory, spec.FileName);
                if (!File.Exists(path))
                {
                    Skip(outcome, sw, "media file not found: " + path);
                    return;
                }

                media = LibVlc.libvlc_media_new_path(_instance, path);
                mp = LibVlc.libvlc_media_player_new_from_media(media);
                cap = new VideoFrameCapture(mp, 320, 180);
                long logCursor = CaptureLogCursor();

                if (!PlayAndWaitPlaying(mp))
                {
                    Fail(outcome, sw, "did not reach Playing state");
                    return;
                }

                Thread.Sleep(1500);
                DescribeTracks(media, outcome);
                LibVlc.libvlc_media_player_set_position(mp, 0.4f);
                Thread.Sleep(800);

                FrameStats stats = cap.Analyze();
                outcome.Details.Add(stats.ToString());

                string bmp = Path.Combine(_env.OutputDirectory, "snapshot-" + GetArtifactStem(spec.FileName) + ".bmp");
                if (cap.WriteBmp(bmp))
                {
                    outcome.Artifacts.Add(bmp);
                }

                bool mediaOk = stats.Frames > 0 && stats.NonDarkFraction > 0.02 && stats.LumaRange > 40 && stats.AvgChannelSpread < 80;
                bool moduleOk = ValidateExpectedModules(spec, ReadLogSince(logCursor), outcome);
                bool ok = mediaOk && moduleOk;
                outcome.Passed = ok;
                outcome.Message = ok
                    ? $"decoded {stats.Frames} frames; {stats.NonDarkFraction:P0} non-dark"
                    : !mediaOk
                        ? "no decoded frames, frame is black, image lacks detail, or the frame has an unexpected color cast"
                        : "expected decoder module was not observed in the VLC log";
            }
            catch (Exception ex)
            {
                outcome.Passed = false;
                outcome.Message = ex.Message;
            }
            finally
            {
                // Stop and release the player BEFORE freeing the capture buffer.
                // Disposing first frees the single shared vmem buffer while the
                // decoder/vout threads may still be running, so a late lock/write
                // hits freed memory -- an intermittent use-after-free that
                // surfaced as a SIGSEGV on Linux (win/osx merely got lucky).
                Cleanup(mp, media);
                cap?.Dispose();
                Record(outcome, sw);
            }
        }

        // ----- Audio -------------------------------------------------------

        private void TestAudioFile(MediaSpec spec)
        {
            var outcome = new TestOutcome("Audio", spec.FileName);
            var sw = Stopwatch.StartNew();
            IntPtr media = IntPtr.Zero, mp = IntPtr.Zero;
            AudioProbe? probe = null;
            try
            {
                string path = Path.Combine(_env.MediaDirectory, spec.FileName);
                if (!File.Exists(path))
                {
                    Skip(outcome, sw, "media file not found: " + path);
                    return;
                }

                media = LibVlc.libvlc_media_new_path(_instance, path);
                mp = LibVlc.libvlc_media_player_new_from_media(media);
                probe = new AudioProbe(mp, 44100, (uint)Math.Max(1, spec.AudioChannels));
                long logCursor = CaptureLogCursor();

                if (!PlayAndWaitPlaying(mp))
                {
                    Fail(outcome, sw, "did not reach Playing state");
                    return;
                }

                Thread.Sleep(1600);
                DescribeTracks(media, outcome);

                double rms = probe.Rms;
                outcome.Details.Add($"captured {probe.SampleValues} samples; RMS={rms:F1}");

                string wav = Path.Combine(_env.OutputDirectory, "audio-" + GetArtifactStem(spec.FileName) + ".wav");
                if (probe.WriteWav(wav))
                {
                    outcome.Artifacts.Add(wav);
                }

                bool audioOk = probe.SampleValues > 0 && rms > 30.0;
                bool moduleOk = ValidateExpectedModules(spec, ReadLogSince(logCursor), outcome);
                bool ok = audioOk && moduleOk;
                outcome.Passed = ok;
                outcome.Message = ok
                    ? $"audio present (RMS {rms:F0})"
                    : !audioOk
                        ? "silent or no audio captured"
                        : "expected decoder module was not observed in the VLC log";
            }
            catch (Exception ex)
            {
                outcome.Passed = false;
                outcome.Message = ex.Message;
            }
            finally
            {
                // Stop/release the player before freeing the amem buffer (see the
                // video test above -- avoids a use-after-free on the audio path).
                Cleanup(mp, media);
                probe?.Dispose();
                Record(outcome, sw);
            }
        }

        // ----- Parsing -----------------------------------------------------

        private void TestParse(MediaSpec spec)
        {
            var outcome = new TestOutcome("Parse", spec.FileName);
            var sw = Stopwatch.StartNew();
            IntPtr media = IntPtr.Zero;
            try
            {
                string path = Path.Combine(_env.MediaDirectory, spec.FileName);
                if (!File.Exists(path))
                {
                    Skip(outcome, sw, "media file not found");
                    return;
                }

                media = LibVlc.libvlc_media_new_path(_instance, path);
                LibVlc.libvlc_media_parse_with_options(media, VlcMediaParseFlag.ParseLocal, 5000);
                Wait(() => LibVlc.libvlc_media_get_parsed_status(media) == VlcMediaParsedStatus.Done, 6000);

                VlcMediaParsedStatus status = LibVlc.libvlc_media_get_parsed_status(media);
                long duration = LibVlc.libvlc_media_get_duration(media);
                var tracks = GetTracks(media);
                outcome.Details.Add($"status={status}, duration={duration}ms, tracks={tracks.Count}");

                bool ok = status == VlcMediaParsedStatus.Done && tracks.Count >= 2;
                outcome.Passed = ok;
                outcome.Message = ok ? "parsed with expected track count" : "parse incomplete";
            }
            catch (Exception ex)
            {
                outcome.Passed = false;
                outcome.Message = ex.Message;
            }
            finally
            {
                if (media != IntPtr.Zero)
                {
                    LibVlc.libvlc_media_release(media);
                }
                Record(outcome, sw);
            }
        }

        // ----- Multitrack audio + subtitles --------------------------------

        private void TestMultitrackAudioAndSubtitles()
        {
            MediaSpec spec = MediaCatalog.Multitrack;
            var outcome = new TestOutcome("Tracks", spec.FileName);
            var sw = Stopwatch.StartNew();
            IntPtr media = IntPtr.Zero, mp = IntPtr.Zero;
            try
            {
                string path = Path.Combine(_env.MediaDirectory, spec.FileName);
                if (!File.Exists(path))
                {
                    Skip(outcome, sw, "media file not found");
                    return;
                }

                media = LibVlc.libvlc_media_new_path(_instance, path);
                mp = LibVlc.libvlc_media_player_new_from_media(media);
                if (!PlayAndWaitPlaying(mp))
                {
                    Fail(outcome, sw, "did not reach Playing state");
                    return;
                }
                Thread.Sleep(1200);

                int audioCount = LibVlc.libvlc_audio_get_track_count(mp);
                int spuCount = LibVlc.libvlc_video_get_spu_count(mp);
                outcome.Details.Add($"audio tracks={audioCount}, spu tracks={spuCount}");

                var audioIds = GetTrackIds(LibVlc.libvlc_audio_get_track_description(mp));
                bool audioSwitch = false;
                if (audioIds.Count >= 2)
                {
                    LibVlc.libvlc_audio_set_track(mp, audioIds[audioIds.Count - 1]);
                    Thread.Sleep(300);
                    audioSwitch = LibVlc.libvlc_audio_get_track(mp) == audioIds[audioIds.Count - 1];
                    outcome.Details.Add("audio switch ok=" + audioSwitch);
                }

                var spuIds = GetTrackIds(LibVlc.libvlc_video_get_spu_description(mp));
                bool spuSwitch = false;
                if (spuIds.Count >= 2)
                {
                    int target = spuIds[spuIds.Count - 1];
                    LibVlc.libvlc_video_set_spu(mp, target);
                    Thread.Sleep(300);
                    spuSwitch = LibVlc.libvlc_video_get_spu(mp) == target;
                    outcome.Details.Add("spu switch ok=" + spuSwitch);
                }

                // audioCount/spuCount may or may not count the "disable" entry; require >= expected.
                bool ok = audioCount >= 2 && spuCount >= 2 && audioSwitch && spuSwitch;
                outcome.Passed = ok;
                outcome.Message = ok
                    ? "two audio + two subtitle tracks selectable"
                    : "expected 2 audio and 2 subtitle tracks that switch";
            }
            catch (Exception ex)
            {
                outcome.Passed = false;
                outcome.Message = ex.Message;
            }
            finally
            {
                Cleanup(mp, media);
                Record(outcome, sw);
            }
        }

        // ----- External subtitles ------------------------------------------

        private void TestExternalSubtitles()
        {
            foreach (string sub in MediaCatalog.ExternalSubtitles)
            {
                var outcome = new TestOutcome("Subtitles", sub);
                var sw = Stopwatch.StartNew();
                IntPtr media = IntPtr.Zero, mp = IntPtr.Zero;
                try
                {
                    string video = Path.Combine(_env.MediaDirectory, MediaCatalog.Videos[0].FileName);
                    string subPath = Path.Combine(_env.MediaDirectory, sub);
                    if (!File.Exists(video) || !File.Exists(subPath))
                    {
                        Skip(outcome, sw, "media/subtitle not found");
                        continue;
                    }

                    media = LibVlc.libvlc_media_new_path(_instance, video);
                    mp = LibVlc.libvlc_media_player_new_from_media(media);
                    if (!PlayAndWaitPlaying(mp))
                    {
                        Fail(outcome, sw, "did not reach Playing state");
                        continue;
                    }
                    Thread.Sleep(600);

                    int before = LibVlc.libvlc_video_get_spu_count(mp);
                    string uri = new Uri(subPath).AbsoluteUri;
                    int added = LibVlc.libvlc_media_player_add_slave(mp, VlcMediaSlaveType.Subtitle, uri, true);
                    Thread.Sleep(700);
                    int after = LibVlc.libvlc_video_get_spu_count(mp);
                    outcome.Details.Add($"add_slave rc={added}, spu {before} -> {after}, selected spu={LibVlc.libvlc_video_get_spu(mp)}");

                    bool ok = added == 0 && after > before;
                    outcome.Passed = ok;
                    outcome.Message = ok ? "external subtitle attached and selectable" : "subtitle slave not added";
                }
                catch (Exception ex)
                {
                    outcome.Passed = false;
                    outcome.Message = ex.Message;
                }
                finally
                {
                    Cleanup(mp, media);
                    Record(outcome, sw);
                }
            }
        }

        // ----- Transport controls ------------------------------------------

        private void TestTransportControls()
        {
            var outcome = new TestOutcome("Transport", "seek/pause/rate");
            var sw = Stopwatch.StartNew();
            IntPtr media = IntPtr.Zero, mp = IntPtr.Zero;
            try
            {
                string path = Path.Combine(_env.MediaDirectory, MediaCatalog.Videos[0].FileName);
                if (!File.Exists(path))
                {
                    Skip(outcome, sw, "media file not found");
                    return;
                }

                media = LibVlc.libvlc_media_new_path(_instance, path);
                mp = LibVlc.libvlc_media_player_new_from_media(media);
                if (!PlayAndWaitPlaying(mp))
                {
                    Fail(outcome, sw, "did not reach Playing state");
                    return;
                }
                Thread.Sleep(600);

                long length = LibVlc.libvlc_media_player_get_length(mp);
                bool seekable = LibVlc.libvlc_media_player_is_seekable(mp) != 0;
                LibVlc.libvlc_media_player_set_position(mp, 0.5f);
                Thread.Sleep(700);
                long time = LibVlc.libvlc_media_player_get_time(mp);

                LibVlc.libvlc_media_player_set_pause(mp, 1);
                Thread.Sleep(300);
                bool paused = LibVlc.libvlc_media_player_get_state(mp) == VlcState.Paused;

                LibVlc.libvlc_media_player_set_pause(mp, 0);
                int rateRc = LibVlc.libvlc_media_player_set_rate(mp, 2.0f);
                float rate = LibVlc.libvlc_media_player_get_rate(mp);

                outcome.Details.Add($"length={length}ms, seekable={seekable}, time@50%={time}ms, paused={paused}, rate={rate}");

                bool ok = length > 0 && seekable && time > length / 5 && paused && rateRc == 0 && Math.Abs(rate - 2.0f) < 0.01f;
                outcome.Passed = ok;
                outcome.Message = ok ? "seek, pause and rate all behaved" : "a transport control did not behave as expected";
            }
            catch (Exception ex)
            {
                outcome.Passed = false;
                outcome.Message = ex.Message;
            }
            finally
            {
                Cleanup(mp, media);
                Record(outcome, sw);
            }
        }

        private void TestStreamOutputTranscode()
        {
            var outcome = new TestOutcome("StreamOutput", "ffmpeg transcode");
            var sw = Stopwatch.StartNew();
            IntPtr media = IntPtr.Zero, mp = IntPtr.Zero, outputMedia = IntPtr.Zero;
            try
            {
                string input = Path.Combine(_env.MediaDirectory, MediaCatalog.Videos[0].FileName);
                if (!File.Exists(input))
                {
                    Skip(outcome, sw, "input media not found");
                    return;
                }

                string output = Path.Combine(_env.OutputDirectory, "sout-avformat-mp4.mp4");
                if (File.Exists(output))
                {
                    File.Delete(output);
                }

                media = LibVlc.libvlc_media_new_path(_instance, input);
                LibVlc.libvlc_media_add_option(media,
                    ":sout=#transcode{vcodec=mp4v,vb=900,acodec=mp4a,ab=128}:std{access=file,mux=avformat{mux=mp4},dst='" + EscapeSoutPath(output) + "'}");
                mp = LibVlc.libvlc_media_player_new_from_media(media);
                long logCursor = CaptureLogCursor();

                if (LibVlc.libvlc_media_player_play(mp) != 0)
                {
                    Fail(outcome, sw, "could not start stream output transcode");
                    return;
                }

                bool completed = Wait(() =>
                {
                    VlcState state = LibVlc.libvlc_media_player_get_state(mp);
                    return state == VlcState.Ended || state == VlcState.Error || state == VlcState.Stopped;
                }, 30000, 100);

                VlcState finalState = LibVlc.libvlc_media_player_get_state(mp);
                Thread.Sleep(250);

                long size = File.Exists(output) ? new FileInfo(output).Length : 0;
                outcome.Details.Add($"state={finalState}, output-bytes={size}");
                if (size > 0)
                {
                    outcome.Artifacts.Add(output);
                }

                string logDelta = ReadLogSince(logCursor);
                bool sawTranscode = logDelta.IndexOf("transcode", StringComparison.OrdinalIgnoreCase) >= 0;
                bool sawFfmpegMux = logDelta.IndexOf("avformat", StringComparison.OrdinalIgnoreCase) >= 0
                    || logDelta.IndexOf("ffmpeg", StringComparison.OrdinalIgnoreCase) >= 0;
                outcome.Details.Add($"log markers: transcode={sawTranscode}, ffmpeg/avformat={sawFfmpegMux}");

                bool parsedOk = false;
                if (size > 0)
                {
                    outputMedia = LibVlc.libvlc_media_new_path(_instance, output);
                    LibVlc.libvlc_media_parse_with_options(outputMedia, VlcMediaParseFlag.ParseLocal, 5000);
                    Wait(() => LibVlc.libvlc_media_get_parsed_status(outputMedia) == VlcMediaParsedStatus.Done, 6000);
                    int trackCount = GetTracks(outputMedia).Count;
                    long duration = LibVlc.libvlc_media_get_duration(outputMedia);
                    outcome.Details.Add($"output tracks={trackCount}, duration={duration}ms");
                    parsedOk = trackCount >= 2 && duration > 0;
                }

                bool ok = completed && finalState != VlcState.Error && size > 0 && parsedOk && sawTranscode && sawFfmpegMux;
                outcome.Passed = ok;
                outcome.Message = ok
                    ? "stream output produced a parsed MP4 via transcode + avformat"
                    : "stream output did not produce the expected ffmpeg-backed transcode";
            }
            catch (Exception ex)
            {
                outcome.Passed = false;
                outcome.Message = ex.Message;
            }
            finally
            {
                if (outputMedia != IntPtr.Zero)
                {
                    LibVlc.libvlc_media_release(outputMedia);
                }
                Cleanup(mp, media);
                Record(outcome, sw);
            }
        }

        // ----- Log verification --------------------------------------------

        private void TestLogFileWritten()
        {
            var outcome = new TestOutcome("Logging", "vlc-log.txt");
            var sw = Stopwatch.StartNew();
            try
            {
                _log?.Flush();
                if (string.IsNullOrEmpty(_logPath) || !File.Exists(_logPath))
                {
                    Fail(outcome, sw, "log file was not created");
                    return;
                }

                var info = new FileInfo(_logPath);
                outcome.Artifacts.Add(_logPath);
                outcome.Details.Add($"log size={info.Length} bytes");
                bool ok = info.Length > 0;
                outcome.Passed = ok;
                outcome.Message = ok ? "libvlc log written to file" : "log file is empty";
            }
            catch (Exception ex)
            {
                outcome.Passed = false;
                outcome.Message = ex.Message;
            }
            finally
            {
                Record(outcome, sw);
            }
        }

        // ----- Helpers -----------------------------------------------------

        private void DescribeTracks(IntPtr media, TestOutcome outcome)
        {
            foreach (VlcMediaTrack t in GetTracks(media))
            {
                string codec = LibVlc.Utf8ToString(LibVlc.libvlc_media_get_codec_description(t.Type, t.Codec)) ?? "?";
                outcome.Details.Add($"track {t.Id}: {t.Type} {codec} (0x{t.Codec:X8})");
            }
        }

        private static List<VlcMediaTrack> GetTracks(IntPtr media)
        {
            var list = new List<VlcMediaTrack>();
            uint n = LibVlc.libvlc_media_tracks_get(media, out IntPtr arr);
            if (n > 0 && arr != IntPtr.Zero)
            {
                for (int i = 0; i < n; i++)
                {
                    IntPtr tp = Marshal.ReadIntPtr(arr, i * IntPtr.Size);
                    if (tp != IntPtr.Zero)
                    {
                        list.Add(Marshal.PtrToStructure<VlcMediaTrack>(tp));
                    }
                }
                LibVlc.libvlc_media_tracks_release(arr, n);
            }
            return list;
        }

        private static List<int> GetTrackIds(IntPtr descriptionList)
        {
            var ids = new List<int>();
            IntPtr node = descriptionList;
            while (node != IntPtr.Zero)
            {
                VlcTrackDescription d = Marshal.PtrToStructure<VlcTrackDescription>(node);
                ids.Add(d.Id);
                node = d.Next;
            }
            if (descriptionList != IntPtr.Zero)
            {
                LibVlc.libvlc_track_description_list_release(descriptionList);
            }
            return ids;
        }

        private static string GetArtifactStem(string fileName) =>
            Path.GetFileName(fileName).Replace('.', '_');

        private long CaptureLogCursor()
        {
            _log?.Flush();
            return !string.IsNullOrEmpty(_logPath) && File.Exists(_logPath)
                ? new FileInfo(_logPath).Length
                : 0;
        }

        private string ReadLogSince(long offset)
        {
            _log?.Flush();
            if (string.IsNullOrEmpty(_logPath) || !File.Exists(_logPath))
            {
                return string.Empty;
            }

            using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (offset > fs.Length)
            {
                offset = 0;
            }
            fs.Seek(offset, SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8, true);
            return reader.ReadToEnd();
        }

        private bool ValidateExpectedModules(MediaSpec spec, string logDelta, TestOutcome outcome)
        {
            IReadOnlyList<string> markers = GetExpectedModules(spec);
            if (markers.Count == 0)
            {
                return true;
            }

            outcome.Details.Add("expected markers: " + string.Join(", ", markers));
            foreach (string marker in markers)
            {
                if (logDelta.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    outcome.Details.Add("observed marker: " + marker);
                    return true;
                }
            }

            outcome.Details.Add("expected markers not found in VLC log delta");
            return false;
        }

        private static IReadOnlyList<string> GetExpectedModules(MediaSpec spec)
        {
            if (IsAppleMobileOrCatalyst() && spec.ExpectedAppleModuleMarkers.Count > 0)
            {
                return spec.ExpectedAppleModuleMarkers;
            }

            if (IsAppleMobileOrCatalyst())
            {
                return Array.Empty<string>();
            }

            return spec.ExpectedModuleMarkers;
        }

        private static IReadOnlyList<string> GetRequiredModules() =>
            IsAppleMobileOrCatalyst() ? AppleMobileRequiredModules : RequiredModules;

        private HashSet<string> DiscoverAvailableModules()
        {
            var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string manifest = Path.Combine(_env.OutputDirectory, "static-modules.txt");
            if (File.Exists(manifest))
            {
                foreach (string line in File.ReadLines(manifest))
                {
                    string module = line.Trim();
                    if (module.Length > 0)
                    {
                        modules.Add(module);
                    }
                }
            }

            string? pluginRoot = VlcRuntime.PluginPath;
            if (string.IsNullOrWhiteSpace(pluginRoot))
            {
                pluginRoot = Environment.GetEnvironmentVariable("VLC_PLUGIN_PATH");
            }

            if (!string.IsNullOrWhiteSpace(pluginRoot) && Directory.Exists(pluginRoot))
            {
                foreach (string file in Directory.EnumerateFiles(pluginRoot, "*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(file);
                    if (!ext.Equals(".dll", StringComparison.OrdinalIgnoreCase)
                        && !ext.Equals(".so", StringComparison.OrdinalIgnoreCase)
                        && !ext.Equals(".dylib", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    modules.Add(NormalizeModuleName(Path.GetFileNameWithoutExtension(file)));
                }
            }

            return modules;
        }

        private static string NormalizeModuleName(string fileName)
        {
            string name = fileName;
            if (name.StartsWith("lib", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(3);
            }
            if (name.EndsWith("_plugin", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - "_plugin".Length);
            }
            return name;
        }

        private static string EscapeSoutPath(string path) =>
            path.Replace('\\', '/').Replace("'", "\\'");

        private static bool IsAppleMobileOrCatalyst() =>
            OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst();

        private static bool PlayAndWaitPlaying(IntPtr mp, int timeoutMs = 10000)
        {
            if (LibVlc.libvlc_media_player_play(mp) != 0)
            {
                return false;
            }
            Wait(() =>
            {
                VlcState s = LibVlc.libvlc_media_player_get_state(mp);
                return s == VlcState.Playing || s == VlcState.Error || s == VlcState.Ended;
            }, timeoutMs);
            return LibVlc.libvlc_media_player_get_state(mp) == VlcState.Playing;
        }

        private static bool Wait(Func<bool> condition, int timeoutMs, int stepMs = 50)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                {
                    return true;
                }
                Thread.Sleep(stepMs);
            }
            return condition();
        }

        private static void Cleanup(IntPtr mp, IntPtr media)
        {
            if (mp != IntPtr.Zero)
            {
                LibVlc.libvlc_media_player_stop(mp);
                LibVlc.libvlc_media_player_release(mp);
            }
            if (media != IntPtr.Zero)
            {
                LibVlc.libvlc_media_release(media);
            }
        }

        private void Record(TestOutcome outcome, Stopwatch sw)
        {
            outcome.Duration = sw.Elapsed;
            _results.Add(outcome);
            _env.Progress?.Invoke(outcome);
        }

        private void Skip(TestOutcome outcome, Stopwatch sw, string reason)
        {
            outcome.Skipped = true;
            outcome.Message = reason;
            Record(outcome, sw);
        }

        private void Fail(TestOutcome outcome, Stopwatch sw, string reason)
        {
            outcome.Passed = false;
            outcome.Message = reason;
            Record(outcome, sw);
        }

        private void WriteReport()
        {
            var sb = new StringBuilder();
            int pass = 0, fail = 0, skip = 0;
            foreach (TestOutcome o in _results)
            {
                if (o.Skipped) skip++;
                else if (o.Passed) pass++;
                else fail++;

                sb.AppendLine(o.ToString());
                foreach (string d in o.Details)
                {
                    sb.AppendLine("    - " + d);
                }
                foreach (string a in o.Artifacts)
                {
                    sb.AppendLine("    artifact: " + a);
                }
            }
            sb.Insert(0, $"VLCDotNet test report — {pass} passed, {fail} failed, {skip} skipped{Environment.NewLine}{Environment.NewLine}");

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
