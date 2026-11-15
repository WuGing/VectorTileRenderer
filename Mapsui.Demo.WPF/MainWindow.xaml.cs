using Mapsui.Projections;
using Mapsui.Tiling.Layers;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WuGing.VectorTileRenderer;
using IOPath = System.IO.Path;

namespace Mapsui.Demo.WPF
{
    public partial class MainWindow : Window
    {
        private readonly string cachePath = IOPath.Combine(AppContext.BaseDirectory, "tile-cache", "manual-validation");
        private int revision;
        private VectorMbTilesSource currentSource;

        public MainWindow()
        {
            Directory.CreateDirectory(cachePath);
            var revisionFile = IOPath.Combine(cachePath, "manual-revision.txt");
            if (File.Exists(revisionFile)) int.TryParse(File.ReadAllText(revisionFile), out revision);
            VectorMbTilesProvider.ProfileSummaryUpdated += OnProfileSummaryUpdated;
            VectorMbTilesProvider.StartProfiling();
            InitializeComponent();
            var sphericalPoint = SphericalMercator.FromLonLat(8.542693, 47.368659);
            MyMapControl.Map.Navigator.CenterOn(new MPoint(sphericalPoint.x, sphericalPoint.y));
            MyMapControl.Map.Navigator.ZoomToLevel(12);
            ShowCacheStatus("Ready");
        }

        protected override void OnClosed(EventArgs e)
        {
            VectorMbTilesProvider.ProfileSummaryUpdated -= OnProfileSummaryUpdated;
            VectorMbTilesProvider.StopProfiling();
            currentSource?.Dispose();
            base.OnClosed(e);
        }

        private void OnProfileSummaryUpdated(string summary)
        {
            if (Dispatcher.HasShutdownStarted) return;
            Dispatcher.BeginInvoke(new Action(() => perfOverlayText.Text = summary));
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // SelectedIndex can fire before the rest of the XAML names are initialized.
            if (MyMapControl == null) return;
            ReloadMap();
        }

        private void ReloadMap()
        {
            try
            {
                var styleName = (styleBox.SelectedItem as ComboBoxItem)?.Tag as string;
                if (styleName == null) return;
                var source = new VectorMbTilesSource(
                    IOPath.Combine(AppContext.BaseDirectory, "tiles", "zurich.mbtiles"),
                    IOPath.Combine(AppContext.BaseDirectory, "styles", styleName + "-style.json"),
                    cachePath, revision);
                MyMapControl.Map.Layers.Clear();
                currentSource?.Dispose();
                currentSource = source;
                MyMapControl.Map.Layers.Add(new TileLayer(source));
                MyMapControl.Refresh();
                ShowCacheStatus("Reloaded");
            }
            catch (Exception exception)
            {
                ShowCacheStatus("Load failed: " + exception.Message);
            }
        }

        private void ShowCacheStatus(string message)
        {
            if (cacheStatusText != null)
                cacheStatusText.Text = $"{message} | Revision {revision} | Cache: {cachePath}";
        }

        private void Reload_Click(object sender, RoutedEventArgs e) => ReloadMap();

        private void Revision_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var nextRevision = checked(revision + 1);
                File.WriteAllText(IOPath.Combine(cachePath, "manual-revision.txt"), nextRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));
                revision = nextRevision;
                ReloadMap();
            }
            catch (Exception exception)
            {
                ShowCacheStatus("Revision update failed: " + exception.Message);
            }
        }

        private async void Trim_Click(object sender, RoutedEventArgs e) => await MaintainCache(false);

        private async void Clear_Click(object sender, RoutedEventArgs e) => await MaintainCache(true);

        private async Task MaintainCache(bool clear)
        {
            trimButton.IsEnabled = false;
            clearButton.IsEnabled = false;
            ShowCacheStatus("Maintaining cache...");
            try
            {
                var result = await Task.Run(() => TileCache.Prune(cachePath,
                    clear ? 0 : 512L * 1024 * 1024, clear ? null : TimeSpan.FromDays(30)));
                ShowCacheStatus($"Removed {result.DeletedFiles} tiles ({result.DeletedBytes / 1048576.0:0.00} MiB), remaining {result.RemainingBytes / 1048576.0:0.00} MiB, failures {result.FailedFiles}. Reload map to request tiles again");
            }
            catch (Exception exception)
            {
                ShowCacheStatus("Maintenance failed: " + exception.Message);
            }
            finally
            {
                trimButton.IsEnabled = true;
                clearButton.IsEnabled = true;
            }
        }
    }
}
