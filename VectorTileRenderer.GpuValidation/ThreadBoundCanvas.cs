using System.Diagnostics;
using SkiaSharp;

namespace WuGing.VectorTileRenderer.GpuValidation;

// Guard before every native call: a wrong-thread experiment must not invoke undefined GL behavior.
internal sealed class ThreadBoundCanvas(ProbeCanvas inner, WindowsGlContext? host) : ICanvas
{
    public int RejectedCalls { get; private set; }
    public int FinishThreadId { get; private set; }
    public double FinishMs { get; private set; }
    public bool ClipOverflow { get => inner.ClipOverflow; set => inner.ClipOverflow = value; }

    private void Check()
    {
        try { host?.VerifyCurrent(); }
        catch { RejectedCalls++; throw; }
    }

    public void StartDrawing(double x, double y) { Check(); inner.StartDrawing(x, y); }
    public void DrawBackground(Brush style) { Check(); inner.DrawBackground(style); }
    public void DrawLineString(List<Point> geometry, Brush style) { Check(); inner.DrawLineString(geometry, style); }
    public void DrawPolygon(List<Point> geometry, Brush style) { Check(); inner.DrawPolygon(geometry, style); }
    public void DrawPoint(Point geometry, Brush style) { Check(); inner.DrawPoint(geometry, style); }
    public void DrawText(Point geometry, Brush style) { Check(); inner.DrawText(geometry, style); }
    public void DrawTextOnPath(List<Point> geometry, Brush style) { Check(); inner.DrawTextOnPath(geometry, style); }
    public void DrawImage(Stream stream, Brush style) { Check(); inner.DrawImage(stream, style); }
    public void DrawUnknown(List<List<Point>> geometry, Brush style) { Check(); inner.DrawUnknown(geometry, style); }
    public SKBitmap FinishDrawing()
    {
        Check();
        FinishThreadId = Environment.CurrentManagedThreadId;
        var started = Stopwatch.GetTimestamp();
        var result = inner.FinishDrawing();
        FinishMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        return result;
    }
}
