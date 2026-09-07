using System.Diagnostics;
using SkiaSharp;

namespace WuGing.VectorTileRenderer.GpuValidation;

// Uses production drawing/readback. Extra checked readback runs only in correctness cases.
internal sealed class ProbeCanvas(GRContext? context) : SkiaGpuCanvas(context), IDisposable
{
    public bool VerifyReadback { get; set; }
    public bool? ReadbackSucceeded { get; private set; }
    public bool TextureBacked { get; private set; }
    public double ReadbackMs { get; private set; }

    public override void StartDrawing(double width, double height)
    {
        ReleaseSurface();
        ReadbackSucceeded = null;
        TextureBacked = false;
        ReadbackMs = 0;
        base.StartDrawing(width, height);
    }

    protected override void OnBeforeFinishDrawing()
    {
        var started = Stopwatch.GetTimestamp();
        base.OnBeforeFinishDrawing();
        ReadbackMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (VerifyReadback && IsGpuEnabled)
        {
            using var snapshot = surface.Snapshot();
            TextureBacked = snapshot?.IsTextureBacked == true;
            using var verification = new SKBitmap(bitmap.Info);
            ReadbackSucceeded = snapshot != null && snapshot.ReadPixels(verification.Info,
                verification.GetPixels(), verification.RowBytes, 0, 0);
            if (ReadbackSucceeded == true && !bitmap.Bytes.AsSpan().SequenceEqual(verification.Bytes))
            {
                throw new InvalidOperationException("Production bitmap differs from independently checked readback.");
            }
        }
    }

    // The harness retains canvases between requests and owns their output bitmaps.
    // GRContext is borrowed and disposed by the host after all surfaces.
    private SKBitmap? completed;

    public override SKBitmap FinishDrawing() => completed = base.FinishDrawing();

    private void ReleaseSurface()
    {
        completed?.Dispose();
        completed = null;
    }

    protected override void Dispose(bool disposing)
    {
        ReleaseSurface();
        base.Dispose(disposing);
    }
}
