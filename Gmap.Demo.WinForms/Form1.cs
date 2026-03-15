using GMap.NET;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using IOPath = System.IO.Path;

namespace Gmap.Demo.WinForms
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            var gmap = new GMap.NET.WindowsForms.GMapControl();
            gmap.Dock = DockStyle.Fill;
            gmap.Visible = true;
            gmap.Position = new PointLatLng(33.698292, 73.060766);
            gmap.MinZoom = 1;
            gmap.MaxZoom = 17;
            gmap.Zoom = 13;
            gmap.Manager.Mode = AccessMode.ServerAndCache;
            gmap.CanDragMap = true;
            gmap.SelectedAreaFillColor = Color.Transparent;
            gmap.ShowCenter = false;
            gmap.MapProvider = GMap.NET.MapProviders.GoogleMapProvider.Instance;
            gmap.DragButton = System.Windows.Forms.MouseButtons.Left;
            gmap.MouseWheelZoomType = GMap.NET.MouseWheelZoomType.MousePositionAndCenter;
            this.Controls.Add(gmap);

            var mainDir = AppContext.BaseDirectory;

            var provider = new VectorMbTilesProvider(
                IOPath.Combine(mainDir, "tiles", "islamabad.mbtiles"),
                IOPath.Combine(mainDir, "styles", "aliflux-style.json"),
                IOPath.Combine(mainDir, "tile-cache"));
            gmap.MapProvider = provider;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }
    }
}
