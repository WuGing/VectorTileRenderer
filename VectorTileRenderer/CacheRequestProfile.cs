namespace WuGing.VectorTileRenderer;

/// <summary>Timings for a completed RenderCached request, excluding its request-profile callback.</summary>
public sealed class CacheRequestProfile
{
    public int X { get; internal set; }
    public int Y { get; internal set; }
    public double Zoom { get; internal set; }
    public bool CacheHit { get; internal set; }
    public bool HasBitmap { get; internal set; }
    public string ActualBackend { get; internal set; }
    public double LookupMs { get; internal set; }
    public double RenderMs { get; internal set; }
    public double EncodeAndPublishMs { get; internal set; }
    public double TotalMs { get; internal set; }
}
