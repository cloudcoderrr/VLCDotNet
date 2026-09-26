using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using VLCDotNet.Tests.Shared;

namespace VLCDotNet.Tests.Maui
{
    public partial class MainPage : ContentPage
    {
        private readonly ObservableCollection<TestOutcome> _results = new ObservableCollection<TestOutcome>();
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

        private async Task RunAsync()
        {
            RunButton.IsEnabled = false;
            _results.Clear();
            SummaryLabel.Text = "Preparing media…";

            string output = MauiTestHost.OutputDirectory();
            try
            {
                string media = await MauiTestHost.StageMediaAsync();
                MauiTestHost.ConfigurePluginPath();
                await MauiTestHost.StageStaticModuleManifestAsync(output);
                MauiTestHost.DumpNativeLayout(output);

                SummaryLabel.Text = "Running…";
                var env = new TestEnvironment
                {
                    MediaDirectory = media,
                    OutputDirectory = output,
                    Progress = o => MainThread.BeginInvokeOnMainThread(() => _results.Add(o)),
                };

                var results = await Task.Run(() => new VlcTestSuite(env).Run());

                int pass = results.Count(r => r.Passed);
                int fail = results.Count(r => !r.Passed && !r.Skipped);
                int skip = results.Count(r => r.Skipped);
                SummaryLabel.Text = $"{pass} passed, {fail} failed, {skip} skipped — {output}";
                WriteMarker(output, $"{pass} passed, {fail} failed, {skip} skipped", fail);
            }
            catch (Exception ex)
            {
                SummaryLabel.Text = "Error: " + ex.Message;
                WriteMarker(output, "error: " + ex, 99);
            }
            finally
            {
                RunButton.IsEnabled = true;
            }
        }

        /// <summary>Writes a completion marker CI can poll for, then pull the artifacts.</summary>
        private static void WriteMarker(string output, string summary, int failures)
        {
            try
            {
                File.WriteAllText(Path.Combine(output, "DONE.txt"), failures + "\n" + summary + "\n");
            }
            catch
            {
                // best effort
            }
        }
    }
}
