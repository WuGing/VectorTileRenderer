using SkiaSharp;

namespace WuGing.VectorTileRenderer;

internal sealed class PlacedText : IDisposable
{
    private readonly List<SKTextBlob> blobs = new();
    public SKRect Bounds { get; private set; }
    public void Add(SKTextBlob blob)
    {
        if (blob == null) return;
        Bounds = blobs.Count == 0 ? blob.Bounds : SKRect.Union(Bounds, blob.Bounds);
        blobs.Add(blob);
    }
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        foreach (var blob in blobs) canvas.DrawText(blob, 0, 0, paint);
    }
    public void Dispose()
    {
        foreach (var blob in blobs) blob.Dispose();
        blobs.Clear();
    }
}
