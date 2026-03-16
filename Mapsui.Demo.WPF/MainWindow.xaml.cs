using Mapsui.Projections;
using Mapsui.Tiling.Layers;
using System;
using IOPath = System.IO.Path;
using System.Windows;
using System.Windows.Controls;

namespace Mapsui.Demo.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            VectorMbTilesProvider.ProfileSummaryUpdated += OnProfileSummaryUpdated;

            var sphericalPoint = SphericalMercator.FromLonLat(8.542693, 47.368659);
            MyMapControl.Map.Navigator.CenterOn(new MPoint(sphericalPoint.x, sphericalPoint.y));
            MyMapControl.Map.Navigator.ZoomToLevel(12);
            
        }

        protected override void OnClosed(EventArgs e)
        {
            VectorMbTilesProvider.ProfileSummaryUpdated -= OnProfileSummaryUpdated;
            base.OnClosed(e);
        }

        private void OnProfileSummaryUpdated(string summary)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                perfOverlayText.Text = summary;
            }));
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var styleName = (styleBox.SelectedItem as ComboBoxItem).Tag as string;
            var mainDir = AppContext.BaseDirectory;

            var source = new VectorMbTilesSource(
                IOPath.Combine(mainDir, "tiles", "zurich.mbtiles"),
                IOPath.Combine(mainDir, "styles", styleName + "-style.json"),
                IOPath.Combine(mainDir, "tile-cache"));
            MyMapControl.Map.Layers.Clear();
            MyMapControl.Map.Layers.Add(new TileLayer(source));
            MyMapControl.Refresh();
        }
    }
}
