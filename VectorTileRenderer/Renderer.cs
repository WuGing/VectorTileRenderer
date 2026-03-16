using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public class RenderProfile
        {
            public int X { get; set; }
            public int Y { get; set; }
            public double Zoom { get; set; }
            public double BuildVisualLayersMs { get; set; }
            public double TileFetchDecodeMs { get; set; }
            public double BuildStyleEvalMs { get; set; }
            public double DrawGeometryMs { get; set; }
            public double DrawTextMs { get; set; }
            public double TotalMs { get; set; }
            public int VisualLayerCount { get; set; }
            public int GeometryDrawCallCount { get; set; }
            public int TextDrawCallCount { get; set; }
            public int FeatureCandidateCount { get; set; }
            public int FeatureAcceptedCount { get; set; }
        }

        // Optional callback for measuring hot-path timings in real app runs.
        public static Action<RenderProfile> ProfileSink { get; set; }

        static double elapsedMs(long startTimestamp, long endTimestamp)
        {
            return (endTimestamp - startTimestamp) * 1000.0 / Stopwatch.Frequency;
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
            var profileSink = ProfileSink;
            var profilingEnabled = profileSink != null;

            long totalStart = 0;
            long buildStart = 0;
            long buildEnd = 0;
            long geometryStart = 0;
            long geometryEnd = 0;
            long textStart = 0;
            long textEnd = 0;

            int geometryDrawCallCount = 0;
            int textDrawCallCount = 0;
            int featureCandidateCount = 0;
            int featureAcceptedCount = 0;
            double tileFetchDecodeMs = 0;
            double buildStyleEvalMs = 0;

            if (profilingEnabled)
            {
                totalStart = Stopwatch.GetTimestamp();
                buildStart = totalStart;
            }

            Dictionary<Source, Stream> rasterTileCache = new Dictionary<Source, Stream>();
            Dictionary<Source, VectorTile> vectorTileCache = new Dictionary<Source, VectorTile>();
            Dictionary<string, List<VectorTileLayer>> categorizedVectorLayers = new Dictionary<string, List<VectorTileLayer>>();
            Dictionary<VectorTileLayer, Dictionary<string, List<VectorTileFeature>>> tileLayerGeometryBuckets = new Dictionary<VectorTileLayer, Dictionary<string, List<VectorTileFeature>>>();
            Dictionary<VectorTileFeature, List<List<Point>>> localizedGeometryCache = new Dictionary<VectorTileFeature, List<List<Point>>>();
            Dictionary<VectorTileFeature, Dictionary<string, object>> featureAttributesCache = new Dictionary<VectorTileFeature, Dictionary<string, object>>();
            HashSet<string> whiteListLayerSet = whiteListLayers == null ? null : new HashSet<string>(whiteListLayers);

            double actualZoom = zoom;

            if (sizeX < 1024)
            {
                var ratio = 1024 / sizeX;
                var zoomDelta = Math.Log(ratio, 2);

                actualZoom = zoom - zoomDelta;
            }

            Dictionary<string, object> rasterAttributes = new Dictionary<string, object>()
            {
                ["$zoom"] = actualZoom,
            };

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
                    if (layer.Source.Type == "vector" && !style.ValidateLayer(layer, actualZoom, null))
                    {
                        continue;
                    }

                    if (layer.Source.Type == "vector")
                    {
                        if (!vectorTileCache.TryGetValue(layer.Source, out var vectorTile))
                        {
                            if (layer.Source.Provider is Sources.IVectorTileSource)
                            {
                                long fetchStart = 0;
                                if (profilingEnabled)
                                {
                                    fetchStart = Stopwatch.GetTimestamp();
                                }

                                var tile = await (layer.Source.Provider as Sources.IVectorTileSource).GetVectorTile(x, y, (int)zoom);

                                if (profilingEnabled)
                                {
                                    tileFetchDecodeMs += elapsedMs(fetchStart, Stopwatch.GetTimestamp());
                                }

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
                                    if (!categorizedVectorLayers.TryGetValue(tileLayer.Name, out var layersByName))
                                    {
                                        layersByName = new List<VectorTileLayer>();
                                        categorizedVectorLayers[tileLayer.Name] = layersByName;
                                    }

                                    layersByName.Add(tileLayer);
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
                                    long fetchStart = 0;
                                    if (profilingEnabled)
                                    {
                                        fetchStart = Stopwatch.GetTimestamp();
                                    }

                                    var tile = await layer.Source.Provider.GetTile(x, y, (int)zoom);

                                    if (profilingEnabled)
                                    {
                                        tileFetchDecodeMs += elapsedMs(fetchStart, Stopwatch.GetTimestamp());
                                    }

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
                                long styleEvalStart = 0;
                                if (profilingEnabled)
                                {
                                    styleEvalStart = Stopwatch.GetTimestamp();
                                }

                                var brush = style.ParseStyle(layer, scale, rasterAttributes);

                                if (profilingEnabled)
                                {
                                    buildStyleEvalMs += elapsedMs(styleEvalStart, Stopwatch.GetTimestamp());
                                }

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
                        var layerNeedsFeatureAttributes = style.NeedsFeatureAttributes(layer);
                        Brush staticLayerBrush = null;

                        if (!layerNeedsFeatureAttributes)
                        {
                            long styleEvalStart = 0;
                            if (profilingEnabled)
                            {
                                styleEvalStart = Stopwatch.GetTimestamp();
                            }

                            staticLayerBrush = style.ParseStyle(layer, scale, new Dictionary<string, object>
                            {
                                ["$zoom"] = actualZoom,
                                ["$id"] = layer.ID,
                            });

                            if (profilingEnabled)
                            {
                                buildStyleEvalMs += elapsedMs(styleEvalStart, Stopwatch.GetTimestamp());
                            }

                            if (!staticLayerBrush.Paint.Visibility)
                            {
                                continue;
                            }
                        }

                        foreach (var tileLayer in tileLayers)
                        {
                            var requiredGeometryType = GetRequiredGeometryTypeForLayer(layer.Type);
                            IEnumerable<VectorTileFeature> candidateFeatures = tileLayer.Features;

                            if (requiredGeometryType != null)
                            {
                                if (!tileLayerGeometryBuckets.TryGetValue(tileLayer, out var geometryBuckets))
                                {
                                    geometryBuckets = BuildGeometryBuckets(tileLayer.Features);
                                    tileLayerGeometryBuckets[tileLayer] = geometryBuckets;
                                }

                                if (!geometryBuckets.TryGetValue(requiredGeometryType, out var geometryFeatures))
                                {
                                    continue;
                                }

                                candidateFeatures = geometryFeatures;
                            }

                            foreach (var feature in candidateFeatures)
                            {
                                if (profilingEnabled)
                                {
                                    featureCandidateCount++;
                                }

                                if (!layerNeedsFeatureAttributes)
                                {
                                    if (!localizedGeometryCache.TryGetValue(feature, out var localizedGeometry))
                                    {
                                        localizedGeometry = localizeGeometry(feature.Geometry, sizeX, sizeY, feature.Extent);
                                        localizedGeometryCache[feature] = localizedGeometry;
                                    }

                                    if (profilingEnabled)
                                    {
                                        featureAcceptedCount++;
                                    }

                                    visualLayers.Add(new VisualLayer()
                                    {
                                        Type = VisualLayerType.Vector,
                                        VectorTileFeature = feature,
                                        Geometry = localizedGeometry,
                                        Brush = staticLayerBrush,
                                    });

                                    continue;
                                }

                                if (!featureAttributesCache.TryGetValue(feature, out var attributes))
                                {
                                    attributes = new Dictionary<string, object>(feature.Attributes.Count + 3);
                                    foreach (var pair in feature.Attributes)
                                    {
                                        attributes[pair.Key] = pair.Value;
                                    }

                                    attributes["$type"] = feature.GeometryType;
                                    attributes["$zoom"] = actualZoom;

                                    featureAttributesCache[feature] = attributes;
                                }

                                attributes["$id"] = layer.ID;

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

                                long styleEvalStart = 0;
                                if (profilingEnabled)
                                {
                                    styleEvalStart = Stopwatch.GetTimestamp();
                                }

                                if (style.ValidateLayer(layer, actualZoom, attributes))
                                {
                                    var brush = style.ParseStyle(layer, scale, attributes);

                                    if (profilingEnabled)
                                    {
                                        buildStyleEvalMs += elapsedMs(styleEvalStart, Stopwatch.GetTimestamp());
                                    }

                                    if (!brush.Paint.Visibility)
                                    {
                                        continue;
                                    }

                                    if (profilingEnabled)
                                    {
                                        featureAcceptedCount++;
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
                                else if (profilingEnabled)
                                {
                                    buildStyleEvalMs += elapsedMs(styleEvalStart, Stopwatch.GetTimestamp());
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

            if (profilingEnabled)
            {
                buildEnd = Stopwatch.GetTimestamp();
                geometryStart = buildEnd;
            }

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
                                    if (profilingEnabled)
                                    {
                                        geometryDrawCallCount++;
                                    }
                                }
                            }
                        }
                        else if (feature.GeometryType == "LineString")
                        {
                            foreach (var line in geometry)
                            {
                                canvas.DrawLineString(line, brush);
                                if (profilingEnabled)
                                {
                                    geometryDrawCallCount++;
                                }
                            }
                        }
                        else if (feature.GeometryType == "Polygon")
                        {

                            foreach (var polygon in geometry)
                            {
                                canvas.DrawPolygon(polygon, brush);
                                if (profilingEnabled)
                                {
                                    geometryDrawCallCount++;
                                }
                            }
                        }
                        else if (feature.GeometryType == "Unknown")
                        {
                            canvas.DrawUnknown(geometry, brush);
                            if (profilingEnabled)
                            {
                                geometryDrawCallCount++;
                            }
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

            if (profilingEnabled)
            {
                geometryEnd = Stopwatch.GetTimestamp();
                textStart = geometryEnd;
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
                                if (profilingEnabled)
                                {
                                    textDrawCallCount++;
                                }
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
                                if (profilingEnabled)
                                {
                                    textDrawCallCount++;
                                }
                            }
                        }
                    }
                }
            }

            var bitmap = canvas.FinishDrawing();

            if (profilingEnabled)
            {
                textEnd = Stopwatch.GetTimestamp();

                try
                {
                    profileSink(new RenderProfile
                    {
                        X = x,
                        Y = y,
                        Zoom = zoom,
                        BuildVisualLayersMs = elapsedMs(buildStart, buildEnd),
                        TileFetchDecodeMs = tileFetchDecodeMs,
                        BuildStyleEvalMs = buildStyleEvalMs,
                        DrawGeometryMs = elapsedMs(geometryStart, geometryEnd),
                        DrawTextMs = elapsedMs(textStart, textEnd),
                        TotalMs = elapsedMs(totalStart, textEnd),
                        VisualLayerCount = orderedVisualLayers.Count,
                        GeometryDrawCallCount = geometryDrawCallCount,
                        TextDrawCallCount = textDrawCallCount,
                        FeatureCandidateCount = featureCandidateCount,
                        FeatureAcceptedCount = featureAcceptedCount,
                    });
                }
                catch (Exception)
                {
                    // Profiling callback failures should never affect rendering.
                }
            }

            return bitmap;
        }

        private static bool IsGeometryCompatibleWithLayer(string layerType, string geometryType)
        {
            if (layerType == "line")
            {
                return geometryType == "LineString";
            }

            if (layerType == "fill")
            {
                return geometryType == "Polygon";
            }

            if (layerType == "circle")
            {
                return geometryType == "Point";
            }

            return true;
        }

        private static string GetRequiredGeometryTypeForLayer(string layerType)
        {
            if (layerType == "line")
            {
                return "LineString";
            }

            if (layerType == "fill")
            {
                return "Polygon";
            }

            if (layerType == "circle")
            {
                return "Point";
            }

            return null;
        }

        private static Dictionary<string, List<VectorTileFeature>> BuildGeometryBuckets(List<VectorTileFeature> features)
        {
            var buckets = new Dictionary<string, List<VectorTileFeature>>(StringComparer.Ordinal);

            foreach (var feature in features)
            {
                if (!buckets.TryGetValue(feature.GeometryType, out var group))
                {
                    group = new List<VectorTileFeature>();
                    buckets[feature.GeometryType] = group;
                }

                group.Add(feature);
            }

            return buckets;
        }

        private static List<List<Point>> localizeGeometry(List<List<Point>> coordinates, double sizeX, double sizeY, double extent)
        {
            var localized = new List<List<Point>>(coordinates.Count);
            var xScale = sizeX / extent;
            var yScale = sizeY / extent;

            foreach (var list in coordinates)
            {
                var localizedList = new List<Point>(list.Count);

                foreach (var point in list)
                {
                    var x = point.X * xScale;
                    var y = point.Y * yScale;

                    localizedList.Add(new Point(x, y));
                }

                localized.Add(localizedList);
            }

            return localized;
        }
    }
}

