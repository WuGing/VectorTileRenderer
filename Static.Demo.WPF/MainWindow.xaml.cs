using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
using WuGing.VectorTileRenderer;

namespace Demo.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        GlobalMercator gmt = new GlobalMercator();
        string mainDir = AppContext.BaseDirectory;

        public MainWindow()
        {
            InitializeComponent();

            // first, we extract necessary pbf tiles from mbtiles db

            var coords = gmt.LatLonToTile(47.371143, 8.543924, 14);
            using (var tileSource = new WuGing.VectorTileRenderer.Sources.SingleMbTilesSource(mainDir + @"tiles/zurich.mbtiles"))
            {
                tileSource.ExtractTile(coords.X, coords.Y, 14, mainDir + @"tiles/zurich.pbf.gz");
            }

            coords = gmt.LatLonToTile(33.693189, 73.061415, 11);
            using (var tileSource = new WuGing.VectorTileRenderer.Sources.SingleMbTilesSource(mainDir + @"tiles/islamabad.mbtiles"))
            {
                tileSource.ExtractTile(coords.X, coords.Y, 11, mainDir + @"tiles/islamabad.pbf.gz");
            }
        }

        private async void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton { Tag: string functionName })
            {
                return;
            }

            // use a little reflection to call example function by name ;)
            MethodInfo theMethod = this.GetType().GetMethod(functionName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (theMethod == null)
            {
                throw new InvalidOperationException($"Could not find demo method '{functionName}'.");
            }

            if (theMethod.Invoke(this, null) is not Task renderTask)
            {
                throw new InvalidOperationException($"Demo method '{functionName}' must return Task.");
            }

            await renderTask;
        }

        Task zurichMbTilesAliFluxStyle()
        {
            return showMbTiles(mainDir + @"tiles/zurich.mbtiles", mainDir + @"styles/aliflux-style.json", 8579, 10645, 8581, 10647, 14, 512);
        }

        Task zurichMbTilesBasicStyle()
        {
            return showMbTiles(mainDir + @"tiles/zurich.mbtiles", mainDir + @"styles/basic-style.json", 8579, 10645, 8581, 10647, 14, 512);
        }

        Task zurichMbTilesLibertyStyle()
        {
            return showMbTiles(mainDir + @"tiles/zurich.mbtiles", mainDir + @"styles/liberty-style.json", 8579, 10645, 8581, 10647, 14, 512);
        }

        Task zurichMbTilesBrightStyle()
        {
            return showMbTiles(mainDir + @"tiles/zurich.mbtiles", mainDir + @"styles/bright-style.json", 8579, 10645, 8581, 10647, 14, 512);
        }

        Task zurichMbTilesDarkStyle()
        {
            return showMbTiles(mainDir + @"tiles/zurich.mbtiles", mainDir + @"styles/dark-style.json", 8579, 10645, 8581, 10647, 14, 512);
        }

        Task islamabadMbTilesBrightStyle()
        {
            return showMbTiles(mainDir + @"tiles/islamabad.mbtiles", mainDir + @"styles/bright-style.json", 1438, 1226, 1440, 1228, 11, 512);
        }

        Task islamabadMbTilesLightStyle()
        {
            return showMbTiles(mainDir + @"tiles/islamabad.mbtiles", mainDir + @"styles/light-style.json", 1438, 1226, 1440, 1228, 11, 512);
        }

        Task zurichCompositeMbTilesBasicStyle()
        {
            return showCompositeMbTiles(
                [mainDir + @"tiles/zurich.mbtiles", mainDir + @"tiles/islamabad.mbtiles"],
                mainDir + @"styles/basic-style.json",
                8579,
                10645,
                8581,
                10647,
                14,
                512);
        }

        Task islamabadCompositeMbTilesBasicStyle()
        {
            return showCompositeMbTiles(
                [mainDir + @"tiles/zurich.mbtiles", mainDir + @"tiles/islamabad.mbtiles"],
                mainDir + @"styles/basic-style.json",
                1438,
                1226,
                1440,
                1228,
                11,
                512);
        }

        Task guangzhouMbTilesAliFluxStyle()
        {
            //showMbTiles(mainDir + @"tiles/guangzhou.mbtiles", mainDir + @"styles/aliflux-style.json", 416, 288, 418, 290, 9, 512);
            return showMbTiles(@"F:\AliData\C#\FlightMapper\FlightMapper\bin\Debug\tiles\asia.mbtiles", mainDir + @"styles/aliflux-style.json", 368, 311, 373, 313, 9, 512);
        }

        Task zurichPbfBasicStyle()
        {
            return showPbf(mainDir + @"tiles/zurich.pbf.gz", mainDir + @"styles/basic-style.json", 14);
        }

        Task islamabadScalePbfBasicStyle()
        {
            return showPbf(mainDir + @"tiles/islamabad.pbf.gz", mainDir + @"styles/basic-style.json", 11, 512, 2);
        }

        Task islamabadSizePbfBasicStyle()
        {
            return showPbf(mainDir + @"tiles/islamabad.pbf.gz", mainDir + @"styles/basic-style.json", 11, 1024, 1);
        }

        Task newyorkPbfMbStreetsStyle()
        {
            return showPbf(mainDir + @"tiles/newyork-mapbox.pbf", mainDir + @"styles/streets-style.json", 11);
        }

        Task newyorkPbfMbRunnerStyle()
        {
            return showPbf(mainDir + @"tiles/newyork-mapbox.pbf", mainDir + @"styles/Runner-style.json", 11);
        }

        Task zurichOverzoomedMbTilesBasicStyle()
        {
            var coords = gmt.LatLonToTile(47.382047, 8.525868, 16);
            return showMbTiles(mainDir + @"tiles/zurich.mbtiles", mainDir + @"styles/basic-style.json", coords.X, coords.Y, coords.X, coords.Y, 16, 512);
        }

        async Task zurichMbTilesHybridStyle()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // load style and font
            var style = new WuGing.VectorTileRenderer.Style(mainDir + @"styles/hybrid-style.json");
            style.FontDirectory = mainDir + @"styles/fonts/";

            // set pbf as tile provider
            var vectorProvider = new WuGing.VectorTileRenderer.Sources.PbfTileSource(mainDir + @"tiles/zurich.pbf.gz");
            style.SetSourceProvider(0, vectorProvider);

            // load raster source
            var rasterProvider = new WuGing.VectorTileRenderer.Sources.RasterTileSource(mainDir + @"tiles/zurich.jpg");
            style.SetSourceProvider("satellite", rasterProvider);

            // render it on a skia canvas
            var canvas = new SkiaCanvas();
            var bitmapR = await Renderer.Render(style, canvas, 0, 0, 14, 256, 256, 1);
            demoImage.Source = ToBitmapSource(bitmapR);

            scrollViewer.Background = new SolidColorBrush(ToMediaColor(style.GetBackgroundColor(14)));

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Console.WriteLine(elapsedMs + "ms time");
        }

        async Task showPbf(string path, string stylePath, double zoom, double size = 512, double scale = 1)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // load style and font
            var style = new WuGing.VectorTileRenderer.Style(stylePath);
            style.FontDirectory = mainDir + @"styles/fonts/";

            // set pbf as tile provider
            var provider = new WuGing.VectorTileRenderer.Sources.PbfTileSource(path);
            style.SetSourceProvider(0, provider);

            // render it on a skia canvas
            var canvas = new SkiaCanvas();
            var bitmapR = await Renderer.Render(style, canvas, 0, 0, zoom, size, size, scale);
            demoImage.Source = ToBitmapSource(bitmapR);

            scrollViewer.Background = new SolidColorBrush(ToMediaColor(style.GetBackgroundColor(zoom)));

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Console.WriteLine(elapsedMs + "ms time");
        }

        async Task showMbTiles(string path, string stylePath, int minX, int minY, int maxX, int maxY, int zoom, double size = 512, double scale = 1)
        {
            using var provider = new WuGing.VectorTileRenderer.Sources.SingleMbTilesSource(path);
            await showMbTilesSource(provider, stylePath, minX, minY, maxX, maxY, zoom, size, scale);
        }

        async Task showCompositeMbTiles(IReadOnlyList<string> paths, string stylePath, int minX, int minY, int maxX, int maxY, int zoom, double size = 512, double scale = 1)
        {
            using var provider = new WuGing.VectorTileRenderer.Sources.CompositeMbTilesSource(paths);
            await showMbTilesSource(provider, stylePath, minX, minY, maxX, maxY, zoom, size, scale);
        }

#nullable enable
        async Task showMbTilesSource(WuGing.VectorTileRenderer.Sources.IVectorTileSource provider, string stylePath, int minX, int minY, int maxX, int maxY, int zoom, double size, double scale)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // Load one style and attach either a single or composite MBTiles source.
            var style = new WuGing.VectorTileRenderer.Style(stylePath)
            {
                FontDirectory = mainDir + @"styles/fonts/"
            };
            style.SetSourceProvider(0, provider);

            int tilePixelWidth = (int)(size * scale);
            int tilePixelHeight = (int)(size * scale);
            var missingTile = CreateTransparentBitmapSource(tilePixelWidth, tilePixelHeight);
            BitmapSource?[,] bitmapSources = new BitmapSource?[maxX - minX + 1, maxY - minY + 1];

            // Bound concurrency so large ranges do not queue one worker per tile.
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 8)
            };
            await Parallel.ForEachAsync(
                EnumerateTileCoordinates(minX, minY, maxX, maxY),
                parallelOptions,
                async (coordinate, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var canvas = new SkiaCanvas();
                    using SKBitmap? bitmapR = await Renderer.Render(
                        style,
                        canvas,
                        coordinate.X,
                        coordinate.Y,
                        zoom,
                        size,
                        size,
                        scale);
                    bitmapSources[coordinate.X - minX, maxY - coordinate.Y] =
                        ToBitmapSource(bitmapR) ?? missingTile;
                });

            // merge the tiles and show it
            var bitmap = mergeBitmaps(bitmapSources, missingTile);
            demoImage.Source = bitmap;

            scrollViewer.Background = new SolidColorBrush(ToMediaColor(style.GetBackgroundColor(zoom)));

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Console.WriteLine(elapsedMs + "ms time");
        }

        static IEnumerable<(int X, int Y)> EnumerateTileCoordinates(int minX, int minY, int maxX, int maxY)
        {
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    yield return (x, y);
                }
            }
        }

        static BitmapSource CreateTransparentBitmapSource(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            int stride = checked(width * 4);
            byte[] pixels = new byte[checked(stride * height)];
            var bitmap = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Pbgra32,
                null,
                pixels,
                stride);
            bitmap.Freeze();
            return bitmap;
        }

        BitmapSource mergeBitmaps(BitmapSource?[,] bitmapSources, BitmapSource missingTile)
        {
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                for (int x = 0; x < bitmapSources.GetLength(0); x++)
                {
                    for (int y = 0; y < bitmapSources.GetLength(1); y++)
                    {
                        BitmapSource tile = bitmapSources[x, y] ?? missingTile;
                        drawingContext.DrawImage(
                            tile,
                            new System.Windows.Rect(
                                x * missingTile.PixelWidth,
                                y * missingTile.PixelHeight,
                                missingTile.PixelWidth,
                                missingTile.PixelHeight));
                    }
                }
            }

            RenderTargetBitmap bmp = new RenderTargetBitmap(
                checked(bitmapSources.GetLength(0) * missingTile.PixelWidth),
                checked(bitmapSources.GetLength(1) * missingTile.PixelHeight),
                96,
                96,
                PixelFormats.Pbgra32);
            bmp.Render(drawingVisual);
            bmp.Freeze();

            return bmp;
        }

        static System.Windows.Media.Color ToMediaColor(WuGing.VectorTileRenderer.Color color)
        {
            return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        static BitmapSource? ToBitmapSource(SKBitmap? bitmap)
        {
            if (bitmap == null)
            {
                return null;
            }

            using (var image = SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = new MemoryStream(data.ToArray()))
            {
                var result = new BitmapImage();
                result.BeginInit();
                result.CacheOption = BitmapCacheOption.OnLoad;
                result.StreamSource = stream;
                result.EndInit();
                result.Freeze();
                return result;
            }
        }
#nullable restore

        private void demoImage_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
            e.Handled = true;
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Images|*.png;*.bmp;*.jpg";
            if (sfd.ShowDialog() != null)
            {
                string ext = System.IO.Path.GetExtension(sfd.FileName);
                BitmapEncoder encoder = new BmpBitmapEncoder();
                switch (ext)
                {
                    case ".jpg":
                        encoder = new JpegBitmapEncoder();
                        break;
                    case ".bmp":
                        encoder = new PngBitmapEncoder();
                        break;
                }

                encoder.Frames.Add(BitmapFrame.Create(demoImage.Source as BitmapSource));

                using (var fileStream = new System.IO.FileStream(sfd.FileName, System.IO.FileMode.Create))
                {
                    encoder.Save(fileStream);
                }
            }
        }
    }
}
