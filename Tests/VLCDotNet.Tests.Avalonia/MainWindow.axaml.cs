using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using VLCDotNet.Tests.Shared;

namespace VLCDotNet.Tests.Avalonia
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<TestOutcome> _results = new ObservableCollection<TestOutcome>();

        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);
            ResultsList.ItemsSource = _results;
        }

        private async void OnRunClick(object? sender, RoutedEventArgs e)
        {
            RunButton.IsEnabled = false;
            _results.Clear();
            SnapshotImage.Source = null;
            DetailText.Text = string.Empty;
            SummaryText.Text = "Running…";

            TestEnvironment env = DesktopTestHost.CreateEnvironment(o =>
                Dispatcher.UIThread.Post(() => _results.Add(o)));

            var results = await Task.Run(() => new VlcTestSuite(env).Run());

            int passed = results.Count(r => r.Passed);
            int failed = results.Count(r => !r.Passed && !r.Skipped);
            int skipped = results.Count(r => r.Skipped);
            SummaryText.Text = $"{passed} passed, {failed} failed, {skipped} skipped  —  output: {env.OutputDirectory}";
            RunButton.IsEnabled = true;
        }

        private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ResultsList.SelectedItem is not TestOutcome o)
            {
                return;
            }

            DetailTitle.Text = $"{o.Status}   {o.Category} / {o.Name}   ({o.Duration.TotalSeconds:F1}s)";

            var sb = new StringBuilder();
            sb.AppendLine(o.Message);
            if (o.Details.Count > 0)
            {
                sb.AppendLine();
                foreach (string d in o.Details)
                {
                    sb.AppendLine("• " + d);
                }
            }
            if (o.Artifacts.Count > 0)
            {
                sb.AppendLine();
                foreach (string a in o.Artifacts)
                {
                    sb.AppendLine("→ " + a);
                }
            }
            DetailText.Text = sb.ToString();

            string? bmp = o.Artifacts.FirstOrDefault(a => a.EndsWith(".bmp", System.StringComparison.OrdinalIgnoreCase));
            SnapshotImage.Source = bmp != null && File.Exists(bmp) ? new Bitmap(bmp) : null;
        }
    }
}
