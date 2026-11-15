namespace WuGing.VectorTileRenderer;

/// <summary>Observed eligible PNG bytes and deletion outcomes from a cache maintenance pass.</summary>
public sealed class TileCachePruneResult
{
    public long RemainingBytes { get; internal set; }
    public long DeletedBytes { get; internal set; }
    public int DeletedFiles { get; internal set; }
    public int FailedFiles { get; internal set; }
}
