using BenchmarkDotNet.Attributes;
using System.Linq;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Benchmarks;

[MemoryDiagnoser]
public class CompositeSourceBenchmarks
{
    private SingleMbTilesSource source;
    private CompositeMbTilesSource composite;
    private int x;
    private int y;
    private GlobalMercator mercator;

    [Params(2, 8, 32)]
    public int SourceCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        source = new SingleMbTilesSource(FindAsset("tiles", "zurich.mbtiles"));
        var coverage = new GlobalMercator.GeoExtent
        {
            West = -180,
            South = -85,
            East = 180,
            North = 85
        };
        List<MbTilesCoverage> sources = new(SourceCount);
        for (var i = 0; i < SourceCount; i++)
        {
            sources.Add(new MbTilesCoverage(source, i)
            {
                Bounds = coverage,
                MinZoom = 0,
                MaxZoom = 22
            });
        }

        composite = new CompositeMbTilesSource(sources, disposeSources: false);
        mercator = new GlobalMercator();
        var coordinate = mercator.LatLonToTile(47.371143, 8.543924, 14);
        x = coordinate.X;
        y = coordinate.Y;
    }

    [Benchmark(Baseline = true)]
    public IReadOnlyList<MbTilesCoverage> LinqCandidateSelection()
    {
        var tileBounds = mercator.TileLatLonBounds(x, y, 14);
        return composite.Sources
            .Where(source => 14 >= source.MinZoom)
            .Where(source => Intersects(source.Bounds, tileBounds))
            .OrderByDescending(source => source.Priority)
            .ThenBy(source => 14 > source.MaxZoom)
            .ThenBy(source => GetBoundsArea(source.Bounds))
            .ThenBy(source => source.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [Benchmark]
    public IReadOnlyList<MbTilesCoverage> ImperativeCandidateSelection()
    {
        return composite.GetSourcesForTile(x, y, 14);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        composite.Dispose();
        source.Dispose();
    }

    private static string FindAsset(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var parts = new string[relativeParts.Length + 1];
            parts[0] = directory.FullName;
            Array.Copy(relativeParts, 0, parts, 1, relativeParts.Length);
            var candidate = Path.Combine(parts);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the Zurich MBTiles benchmark fixture.");
    }

    private static bool Intersects(GlobalMercator.GeoExtent left, GlobalMercator.GeoExtent right)
    {
        return left.West <= right.East
            && left.East >= right.West
            && left.North >= right.South
            && left.South <= right.North;
    }

    private static double GetBoundsArea(GlobalMercator.GeoExtent bounds)
    {
        return Math.Max(0, bounds.East - bounds.West)
            * Math.Max(0, bounds.North - bounds.South);
    }
}
