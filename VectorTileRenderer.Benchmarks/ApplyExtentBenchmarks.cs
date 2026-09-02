using BenchmarkDotNet.Attributes;

namespace VectorTileRenderer.Benchmarks;

[MemoryDiagnoser]
public class ApplyExtentBenchmarks
{
    private readonly Rect extent = new(0.25, 0.25, 0.5, 0.5);
    private VectorTile tile;

    [Params(100, 1000)]
    public int FeatureCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var layer = new VectorTileLayer { Name = "roads" };
        for (var i = 0; i < FeatureCount; i++)
        {
            layer.Features.Add(new VectorTileFeature
            {
                Extent = 4096,
                GeometryType = "LineString",
                Attributes = new Dictionary<string, object> { ["class"] = "road" },
                Geometry =
                [
                    [
                        new Point(0.25, 0.25),
                        new Point(0.375, 0.5),
                        new Point(0.5, 0.625),
                        new Point(0.75, 0.75)
                    ]
                ]
            });
        }

        tile = new VectorTile { Layers = [layer] };
    }

    [Benchmark(Baseline = true)]
    public VectorTile LegacyConvertRangePerCoordinate()
    {
        var newTile = new VectorTile { IsOverZoomed = tile.IsOverZoomed };

        foreach (var layer in tile.Layers)
        {
            var newLayer = new VectorTileLayer { Name = layer.Name };
            foreach (var feature in layer.Features)
            {
                var newFeature = new VectorTileFeature
                {
                    Attributes = new Dictionary<string, object>(feature.Attributes),
                    Extent = feature.Extent,
                    GeometryType = feature.GeometryType
                };
                var newGeometry = new List<List<Point>>();

                foreach (var geometry in feature.Geometry)
                {
                    var newPoints = new List<Point>();
                    foreach (var point in geometry)
                    {
                        newPoints.Add(new Point(
                            ConvertRange(point.X, extent.Left, extent.Right, 0, feature.Extent),
                            ConvertRange(point.Y, extent.Top, extent.Bottom, 0, feature.Extent)));
                    }

                    newGeometry.Add(newPoints);
                }

                newFeature.Geometry = newGeometry;
                newLayer.Features.Add(newFeature);
            }

            newTile.Layers.Add(newLayer);
        }

        return newTile;
    }

    [Benchmark]
    public VectorTile PrecomputedScaleAndCapacity() => tile.ApplyExtent(extent);

    private static double ConvertRange(double value, double oldMin, double oldMax, double newMin, double newMax)
    {
        var oldRange = oldMax - oldMin;
        return oldRange == 0 ? newMin : ((value - oldMin) * (newMax - newMin) / oldRange) + newMin;
    }
}
