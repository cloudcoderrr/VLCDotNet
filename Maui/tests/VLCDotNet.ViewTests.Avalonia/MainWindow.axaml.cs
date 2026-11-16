using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using VLCDotNet;
using VLCDotNet.ViewTests;

namespace VLCDotNet.ViewTests.Avalonia
{
    public partial class MainWindow : Window
    {
        private readonly LibVLC _libvlc;
        private readonly MediaPlayer _player;

        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);

            _libvlc = new LibVLC("--no-video-title-show");
            _player = new MediaPlayer(_libvlc);
            Video.MediaPlayer = _player;

            string media = Path.Combine(DesktopTestHost.ResolveMediaDirectory(), MediaManifest.Videos[0].FileName);
            if (File.Exists(media))
            {
                _player.Media = new Media(_libvlc, media, FromType.FromPath);
            }

            Closed += OnClosed;
        }

        private void OnPlay(object? sender, RoutedEventArgs e) => _player.Play();

        private void OnPause(object? sender, RoutedEventArgs e) => _player.Pause();

        private void OnStop(object? sender, RoutedEventArgs e) => _player.Stop();

        private void OnSnapshot(object? sender, RoutedEventArgs e)
        {
            string dir = DesktopTestHost.ResolveOutputDirectory();
            _player.TakeSnapshot(0, Path.Combine(dir, "manual-snapshot.png"), 0, 0);
        }

        private async void OnRunTests(object? sender, RoutedEventArgs e)
        {
            RunTestsButton.IsEnabled = false;
            SummaryText.Text = "Running…";

            TestEnvironment env = DesktopTestHost.CreateEnvironment();
            var results = await Task.Run(() => new HighLevelTestSuite(env).Run());

            int passed = results.Count(r => r.Passed);
            int failed = results.Count(r => !r.Passed && !r.Skipped);
            int skipped = results.Count(r => r.Skipped);
            await Dispatcher.UIThread.InvokeAsync(() =>
                SummaryText.Text = $"{passed} passed, {failed} failed, {skipped} skipped — {env.OutputDirectory}");
            RunTestsButton.IsEnabled = true;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _player.Dispose();
            _libvlc.Dispose();
        }
    }
}
