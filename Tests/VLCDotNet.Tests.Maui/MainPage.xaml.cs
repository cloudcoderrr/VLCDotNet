using System.Collections.ObjectModel;
using System.Linq;
using VLCDotNet.Tests.Shared;

namespace VLCDotNet.Tests.Maui
{
    public partial class MainPage : ContentPage
    {
        private readonly ObservableCollection<TestOutcome> _results = new ObservableCollection<TestOutcome>();

        public MainPage()
        {
            InitializeComponent();
            ResultsView.ItemsSource = _results;
        }

        private async void OnRunClicked(object? sender, EventArgs e)
        {
            RunButton.IsEnabled = false;
            _results.Clear();
            SummaryLabel.Text = "Preparing media…";

            try
            {
                string media = await MauiTestHost.StageMediaAsync();
                string output = MauiTestHost.OutputDirectory();
                MauiTestHost.ConfigurePluginPath();

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
            }
            catch (Exception ex)
            {
                SummaryLabel.Text = "Error: " + ex.Message;
            }
            finally
            {
                RunButton.IsEnabled = true;
            }
        }
    }
}
