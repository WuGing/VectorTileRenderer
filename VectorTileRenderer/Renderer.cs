using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SkiaSharp;

namespace VectorTileRenderer
{
    public class Renderer
    {
        // TODO make it instance based... maybe
        private static object cacheLock = new object();

        enum VisualLayerType
        {
            Vector,
            Raster,
        }

        class VisualLayer
        {
            public VisualLayerType Type { get; set; }

            public Stream RasterStream { get; set; } = null;

            public VectorTileFeature VectorTileFeature { get; set; } = null;

            public List<List<Point>> Geometry { get; set; } = null;

            public Brush Brush { get; set; } = null;
        }

        public async static Task<SKBitmap> RenderCached(string cachePath, Style style, ICanvas canvas, int x, int y, double zoom, double sizeX = 512, double sizeY = 512, double scale = 1, List<string> whiteListLayers = null)
        {
            string layerString = whiteListLayers == null ? "" : string.Join(",-", whiteListLayers.ToArray());

            var bundle = new
            {
                style.Hash,
                sizeX,
                sizeY,
                scale,
                layerString,
            };

            lock (cacheLock)
            {
                if (!Directory.Exists(cachePath))
                {
                    Directory.CreateDirectory(cachePath);
                }
            }

            var json = JsonSerializer.Serialize(bundle);
            var hash = Utils.Sha256(json).Substring(0, 12); // get 12 digits to avoid fs length issues

            var fileName = x + "x" + y + "-" + zoom + "-" + hash + ".png";
            var path = Path.Combine(cachePath, fileName);

            lock (cacheLock)
            {
                if (File.Exists(path))
                {
                    return loadBitmap(path);
                }
            }

            var bitmap = await Render(style, canvas, x, y, zoom, sizeX, sizeY, scale, whiteListLayers);

            // save to file in async fashion
            var _t = Task.Run(() =>
            {
                if (bitmap != null)
                {
                    try
                    {
                        lock (cacheLock)
                        {
                            if (File.Exists(path))
                            {
                                return;
                            }

                            using (var fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite))
                            {
                                  using (var image = SKImage.FromBitmap(bitmap))
                                  using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                                  {
                                      data.SaveTo(fileStream);
                                  }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        return;
                    }
                }
            });

            return bitmap;
        }

        static SKBitmap loadBitmap(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return SKBitmap.Decode(stream);
            }
        }

        public async static Task<SKBitmap> Render(Style style, ICanvas canvas, int x, int y, double zoom, double sizeX = 512, double sizeY = 512, double scale = 1, List<string> whiteListLayers = null)
        {
            Dictionary<Source, Stream> rasterTileCache = new Dictionary<Source, Stream>();
            Dictionary<Source, VectorTile> vectorTileCache = new Dictionary<Source, VectorTile>();
            Dictionary<string, List<VectorTileLayer>> categorizedVectorLayers = new Dictionary<string, List<VectorTileLayer>>();
            Dictionary<VectorTileFeature, List<List<Point>>> localizedGeometryCache = new Dictionary<VectorTileFeature, List<List<Point>>>();
            HashSet<string> whiteListLayerSet = whiteListLayers == null ? null : new HashSet<string>(whiteListLayers);

            double actualZoom = zoom;

            if (sizeX < 1024)
            {
                var ratio = 1024 / sizeX;
                var zoomDelta = Math.Log(ratio, 2);

                actualZoom = zoom - zoomDelta;
            }

            sizeX *= scale;
            sizeY *= scale;

            canvas.StartDrawing(sizeX, sizeY);

            var visualLayers = new List<VisualLayer>();

            // TODO refactor this messy block
            foreach (var layer in style.Layers)
            {
                if (whiteListLayerSet != null && layer.Type != "background" && layer.SourceLayer != "")
                {
                    if (!whiteListLayerSet.Contains(layer.SourceLayer))
                    {
                        continue;
                    }
                }
                if (layer.Source != null)
                {
                    if (layer.Source.Type == "vector")
                    {
                        if (!vectorTileCache.TryGetValue(layer.Source, out var vectorTile))
                        {
                            if (layer.Source.Provider is Sources.IVectorTileSource)
                            {
                                var tile = await (layer.Source.Provider as Sources.IVectorTileSource).GetVectorTile(x, y, (int)zoom);

                                if (tile == null)
                                {
                                    return null;
                                    // throwing exceptions screws up the performance
                                    throw new FileNotFoundException("Could not load tile : " + x + "," + y + "," + zoom + " of " + layer.SourceName);
                                }

                                // magic sauce! :p
                                if (tile.IsOverZoomed)
                                {
                                    canvas.ClipOverflow = true;
                                }

                                //canvas.ClipOverflow = true;

                                vectorTileCache[layer.Source] = tile;
                                vectorTile = tile;

                                foreach (var tileLayer in tile.Layers)
                                {
                                    if (!categorizedVectorLayers.ContainsKey(tileLayer.Name))
                                    {
                                        categorizedVectorLayers[tileLayer.Name] = new List<VectorTileLayer>();
                                    }
                                    categorizedVectorLayers[tileLayer.Name].Add(tileLayer);
                                }
                            }
                        }
                    }
                    else if (layer.Source.Type == "raster")
                    {
                        if (!rasterTileCache.TryGetValue(layer.Source, out var rasterTile))
                        {
                            if (layer.Source.Provider != null)
                            {
                                if (layer.Source.Provider is Sources.ITileSource)
                                {
                                    var tile = await layer.Source.Provider.GetTile(x, y, (int)zoom);

                                    if (tile == null)
                                    {
                                        continue;
                                        // throwing exceptions screws up the performance
                                        throw new FileNotFoundException("Could not load tile : " + x + "," + y + "," + zoom + " of " + layer.SourceName);
                                    }

                                    rasterTileCache[layer.Source] = tile;
                                    rasterTile = tile;
                                }
                            }
                        }

                        if (rasterTile != null)
                        {
                            if (style.ValidateLayer(layer, (int)zoom, null))
                            {
                                var brush = style.ParseStyle(layer, scale, new Dictionary<string, object>());

                                if (!brush.Paint.Visibility)
                                {
                                    continue;
                                }

                                visualLayers.Add(new VisualLayer()
                                {
                                    Type = VisualLayerType.Raster,
                                    RasterStream = rasterTile,
                                    Brush = brush,
                                });
                            }
                        }
                    }

                    if (categorizedVectorLayers.TryGetValue(layer.SourceLayer, out var tileLayers))
                    {
                        foreach (var tileLayer in tileLayers)
                        {
                            foreach (var feature in tileLayer.Features)
                            {
                                //var geometry = localizeGeometry(feature.Geometry, sizeX, sizeY, feature.Extent);
                                var attributes = new Dictionary<string, object>(feature.Attributes.Count + 3);
                                foreach (var pair in feature.Attributes)
                                {
                                    attributes[pair.Key] = pair.Value;
                                }

                                attributes["$type"] = feature.GeometryType;
                                attributes["$id"] = layer.ID;
                                attributes["$zoom"] = actualZoom;

                                //if ((string)attributes["$type"] == "Point")
                                //{
                                //    if (attributes.ContainsKey("class"))
                                //    {
                                //        if ((string)attributes["class"] == "country")
                                //        {
                                //            if (layer.ID == "country_label")
                                //            {

                                //            }
                                //        }
                                //    }
                                //}

                                if (style.ValidateLayer(layer, actualZoom, attributes))
                                {
                                    var brush = style.ParseStyle(layer, scale, attributes);

                                    if (!brush.Paint.Visibility)
                                    {
                                        continue;
                                    }

                                    if (!localizedGeometryCache.TryGetValue(feature, out var localizedGeometry))
                                    {
                                        localizedGeometry = localizeGeometry(feature.Geometry, sizeX, sizeY, feature.Extent);
                                        localizedGeometryCache[feature] = localizedGeometry;
                                    }

                                    visualLayers.Add(new VisualLayer()
                                    {
                                        Type = VisualLayerType.Vector,
                                        VectorTileFeature = feature,
                                        Geometry = localizedGeometry,
                                        Brush = brush,
                                    });
                                }
                            }
                        }
                    }
                }
                else if (layer.Type == "background")
                {
                    var brushes = style.GetStyleByType("background", actualZoom, scale);
                    foreach (var brush in brushes)
                    {
                        canvas.DrawBackground(brush);
                    }
                }
            }

            var orderedVisualLayers = visualLayers.OrderBy(item => item.Brush.ZIndex).ToList();

            // defered rendering to preserve text drawing order
            foreach (var layer in orderedVisualLayers)
            {
                if (layer.Type == VisualLayerType.Vector)
                {
                    var feature = layer.VectorTileFeature;
                    var geometry = layer.Geometry;
                    var brush = layer.Brush;

                    if (!brush.Paint.Visibility)
                    {
                        continue;
                    }

                    try
                    {
                        if (feature.GeometryType == "Point")
                        {
                            foreach (var point in geometry)
                            {
                                if (point.Count > 0)
                                {
                                    canvas.DrawPoint(point[0], brush);
                                }
                            }
                        }
                        else if (feature.GeometryType == "LineString")
                        {
                            foreach (var line in geometry)
                            {
                                canvas.DrawLineString(line, brush);
                            }
                        }
                        else if (feature.GeometryType == "Polygon")
                        {

                            foreach (var polygon in geometry)
                            {
                                canvas.DrawPolygon(polygon, brush);
                            }
                        }
                        else if (feature.GeometryType == "Unknown")
                        {
                            canvas.DrawUnknown(geometry, brush);
                        }
                        else
                        {

                        }
                    }
                    catch (Exception)
                    {

                    }
                }
                else if (layer.Type == VisualLayerType.Raster)
                {
                    canvas.DrawImage(layer.RasterStream, layer.Brush);
                    layer.RasterStream.Close();
                }
            }

            for (int i = orderedVisualLayers.Count - 1; i >= 0; i--)
            {
                var layer = orderedVisualLayers[i];
                if (layer.Type == VisualLayerType.Vector)
                {
                    var feature = layer.VectorTileFeature;
                    var geometry = layer.Geometry;
                    var brush = layer.Brush;

                    if (!brush.Paint.Visibility)
                    {
                        continue;
                    }

                    if (feature.GeometryType == "Point")
                    {
                        foreach (var point in geometry)
                        {
                            if (brush.Text != null && point.Count > 0)
                            {
                                canvas.DrawText(point[0], brush);
                            }
                        }
                    }
                    else if (feature.GeometryType == "LineString")
                    {
                        foreach (var line in geometry)
                        {
                            if (brush.Text != null)
                            {
                                canvas.DrawTextOnPath(line, brush);
                            }
                        }
                    }
                }
            }

            return canvas.FinishDrawing();
        }

        private static List<List<Point>> localizeGeometry(List<List<Point>> coordinates, double sizeX, double sizeY, double extent)
        {
            var localized = new List<List<Point>>(coordinates.Count);

            foreach (var list in coordinates)
            {
                var localizedList = new List<Point>(list.Count);

                foreach (var point in list)
                {
                    var x = Utils.ConvertRange(point.X, 0, extent, 0, sizeX, false);
                    var y = Utils.ConvertRange(point.Y, 0, extent, 0, sizeY, false);

                    localizedList.Add(new Point(x, y));
                }

                localized.Add(localizedList);
            }

            return localized;
        }
    }
}

