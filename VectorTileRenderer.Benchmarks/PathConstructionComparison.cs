using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using SkiaSharp;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Benchmarks;

internal static class PathConstructionComparison
{
    public static void Run(string root, string output, int iterations)
    {
        if (iterations < 10) throw new ArgumentOutOfRangeException(nameof(iterations));
        using var source = new SingleMbTilesSource(Path.Combine(root, "tiles", "zurich.mbtiles"));
        var tile = source.GetVectorTile(8580, 10646, 14).GetAwaiter().GetResult();
        var geometries = tile.Layers.SelectMany(layer => layer.Features)
            .Where(feature => feature.GeometryType is "LineString" or "Polygon")
            .SelectMany(feature => feature.Geometry).Where(part => part.Count > 0)
            .Select(part => part.Select(point => new Point(point.X * 512, point.Y * 512)).ToList()).ToArray();
        var batched = typeof(SkiaCanvas).GetMethod("GetPathFromGeometry", BindingFlags.NonPublic | BindingFlags.Static)
            .CreateDelegate<Func<List<Point>, SKPath>>();
        foreach (var geometry in geometries)
        {
            using var expected = Legacy(geometry);
            using var actual = batched(geometry);
            if (actual.FillType != expected.FillType || actual.VerbCount != expected.VerbCount || !actual.Points.SequenceEqual(expected.Points))
                throw new InvalidOperationException("Path data differs.");
        }
        var samples = new List<object>();
        for (var round = -5; round < iterations; round++)
        for (var offset = 0; offset < 2; offset++)
        {
            var candidate = (round + 5 + offset) % 2 == 0;
            Func<List<Point>, SKPath> build = candidate ? batched : Legacy;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            foreach (var geometry in geometries)
            {
                using var path = build(geometry);
            }
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (round >= 0) samples.Add(new { Round = round, Mode = candidate ? "batched" : "legacy", ElapsedMs = elapsed, ManagedBytes = allocated });
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Tile = "zurich-z14-8580-10646", Paths = geometries.Length, Points = geometries.Sum(part => part.Count),
            Iterations = iterations, Warmups = 5, PathDataEquality = true,
            Policy = "Alternating builders on fixed 512-localized geometry. Includes native path disposal in both modes, excludes rendering/source fetch. Managed counters exclude native memory. No profiler.",
            Samples = samples
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(output);
    }

    private static SKPath Legacy(List<Point> geometry)
    {
        var path = new SKPath { FillType = SKPathFillType.EvenOdd };
        path.MoveTo((float)geometry[0].X, (float)geometry[0].Y);
        foreach (var point in geometry.Skip(1)) path.LineTo((float)point.X, (float)point.Y);
        return path;
    }
}
