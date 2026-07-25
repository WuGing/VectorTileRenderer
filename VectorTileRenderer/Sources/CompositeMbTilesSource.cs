using System.Linq;

namespace WuGing.VectorTileRenderer.Sources;

public sealed class CompositeMbTilesSource(IEnumerable<SingleMbTilesSource> sources) : IVectorTileSource, IDisposable
{
    private readonly IReadOnlyList<MbTilesCoverage> sources = [.. sources
        .Select(s => new MbTilesCoverage(s, 0))];
    private readonly GlobalMercator gmt = new();

    public int MinZoom => sources.Min(s => s.MinZoom);
    public int MaxZoom => sources.Max(s => s.MaxZoom);

    public Task<Stream> GetTile(int x, int y, int zoom)
    {
        return Task.FromResult(GetRawTile(x, y, zoom));
    }

    public Stream GetRawTile(int x, int y, int z)
    {
        foreach (var candidate in GetSourcesForTile(x, y, z))
        {
            var tile = candidate.Source.GetRawTile(x, y, z);
            if (tile is not null)
            {
                return tile;
            }
        }

        return null;
    }

    public async Task<VectorTile> GetVectorTile(int x, int y, int z)
    {
        foreach (var candidate in GetSourcesForTile(x, y, z))
        {
            var tile = await candidate.Source.GetVectorTile(x, y, z).ConfigureAwait(false);
            if (tile is not null)
            {
                return tile;
            }
        }

        return null;
    }

    private static bool Intersects(GlobalMercator.GeoExtent a, GlobalMercator.GeoExtent b)
    {
        return a.West <= b.East
            && a.East >= b.West
            && a.North >= b.South
            && a.South <= b.North;
    }

    private IReadOnlyList<MbTilesCoverage> GetSourcesForTile(int x, int y, int z)
    {
        var tileBounds = gmt.TileLatLonBounds(x, y, z);

        var candidates = sources
            .Where(s => z >= s.MinZoom && z <= s.MaxZoom)
            .Where(s => Intersects(s.Bounds, tileBounds))
            .OrderByDescending(s => s.Priority)
            .ThenBy(s => GetBoundsArea(s.Bounds))
            .ToList();

        return candidates;
    }

    private static double GetBoundsArea(GlobalMercator.GeoExtent bounds)
    {
        var width = Math.Max(0, bounds.East - bounds.West);
        var height = Math.Max(0, bounds.North - bounds.South);
        return width * height;
    }

    public void Dispose()
    {
        foreach (var source in sources) 
        {
            source.Source.Dispose();
        }
    }
}
