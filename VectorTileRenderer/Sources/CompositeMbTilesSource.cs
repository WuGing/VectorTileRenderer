using System.Linq;

namespace WuGing.VectorTileRenderer.Sources;

public sealed class CompositeMbTilesSource : IVectorTileSource, IDisposable
{
    private readonly IReadOnlyList<MbTilesCoverage> sources;
    private readonly GlobalMercator gmt = new();
    private readonly bool disposeSources;
    private bool disposed;

    public CompositeMbTilesSource(IEnumerable<string> paths)
        : this(CreateCoverages(paths), true)
    {
    }

    public CompositeMbTilesSource(IEnumerable<SingleMbTilesSource> sources)
        : this(CreateCoverages(sources), true)
    {
    }

    public CompositeMbTilesSource(IEnumerable<MbTilesCoverage> sources, bool disposeSources = true)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        this.sources = [.. sources];
        if (this.sources.Count == 0)
        {
            throw new ArgumentException("At least one MBTiles source is required.", nameof(sources));
        }

        this.disposeSources = disposeSources;
        Bounds = new GlobalMercator.GeoExtent
        {
            West = this.sources.Min(s => s.Bounds.West),
            South = this.sources.Min(s => s.Bounds.South),
            East = this.sources.Max(s => s.Bounds.East),
            North = this.sources.Max(s => s.Bounds.North)
        };
    }

    public IReadOnlyList<MbTilesCoverage> Sources => sources;
    public IReadOnlyList<string> Paths => [.. sources.Select(s => s.Path)];
    public GlobalMercator.GeoExtent Bounds { get; }

    public int MinZoom => sources.Min(s => s.MinZoom);
    public int MaxZoom => sources.Max(s => s.MaxZoom);

    public Task<Stream> GetTile(int x, int y, int zoom)
    {
        return Task.FromResult(GetRawTile(x, y, zoom));
    }

    public Stream GetRawTile(int x, int y, int z)
    {
        ThrowIfDisposed();

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
        ThrowIfDisposed();

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

    internal IReadOnlyList<MbTilesCoverage> GetSourcesForTile(int x, int y, int z)
    {
        ThrowIfDisposed();
        var tileBounds = gmt.TileLatLonBounds(x, y, z);
        List<MbTilesCoverage> candidates = new(sources.Count);

        foreach (var source in sources)
        {
            if (z >= source.MinZoom && Intersects(source.Bounds, tileBounds))
            {
                candidates.Add(source);
            }
        }

        candidates.Sort(new CandidateComparer(z));

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
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (!disposeSources)
        {
            return;
        }

        HashSet<SingleMbTilesSource> disposedSources = [];
        foreach (var source in sources)
        {
            if (disposedSources.Add(source.Source))
            {
                source.Source.Dispose();
            }
        }
    }

    private static IReadOnlyList<MbTilesCoverage> CreateCoverages(IEnumerable<string> paths)
    {
        if (paths is null)
        {
            throw new ArgumentNullException(nameof(paths));
        }

        List<SingleMbTilesSource> createdSources = [];
        try
        {
            foreach (var path in paths)
            {
                createdSources.Add(new SingleMbTilesSource(path));
            }

            return [.. createdSources.Select(source => new MbTilesCoverage(source))];
        }
        catch
        {
            foreach (var source in createdSources)
            {
                source.Dispose();
            }

            throw;
        }
    }

    private static IReadOnlyList<MbTilesCoverage> CreateCoverages(IEnumerable<SingleMbTilesSource> sources)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        return [.. sources.Select(source => new MbTilesCoverage(source))];
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(CompositeMbTilesSource));
        }
    }

    private sealed class CandidateComparer(int zoom) : IComparer<MbTilesCoverage>
    {
        public int Compare(MbTilesCoverage left, MbTilesCoverage right)
        {
            var result = right.Priority.CompareTo(left.Priority);
            if (result != 0)
            {
                return result;
            }

            result = (zoom > left.MaxZoom).CompareTo(zoom > right.MaxZoom);
            if (result != 0)
            {
                return result;
            }

            result = GetBoundsArea(left.Bounds).CompareTo(GetBoundsArea(right.Bounds));
            if (result != 0)
            {
                return result;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(left.Path, right.Path);
        }
    }
}
