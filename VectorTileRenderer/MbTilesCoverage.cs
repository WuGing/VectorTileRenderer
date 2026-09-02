using WuGing.VectorTileRenderer.Sources;

namespace WuGing.VectorTileRenderer;

public sealed class MbTilesCoverage(SingleMbTilesSource source, int priority = 0)
{
    public SingleMbTilesSource Source { get; init; } = source ?? throw new ArgumentNullException(nameof(source));
    public GlobalMercator.GeoExtent Bounds { get; init; } = source.Bounds;
    public int MinZoom { get; init; } = source.MinZoom;
    public int MaxZoom { get; init; } = source.MaxZoom;
    public int Priority { get; init; } = priority;
    public string Path { get; init; } = source.Path;
}
