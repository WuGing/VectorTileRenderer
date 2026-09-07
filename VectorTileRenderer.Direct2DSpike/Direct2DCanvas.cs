#nullable enable
using System.Diagnostics;
using System.Numerics;
using SharpGen.Runtime;
using SkiaSharp;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using WuGing.VectorTileRenderer;
using DColor = Vortice.Mathematics.Color4;
using MapBrush = WuGing.VectorTileRenderer.Brush;
using MapPoint = WuGing.VectorTileRenderer.Point;
using MapColor = WuGing.VectorTileRenderer.Color;

namespace WuGing.VectorTileRenderer.Direct2DSpike;

// Experimental adapter only. Context and native resources are owned on one thread.
internal sealed class Direct2DCanvas : ICanvas, IDisposable
{
    private readonly ID3D11Device d3d = D3D11.D3D11CreateDevice(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
    private readonly ID2D1Factory1 factory = D2D1.D2D1CreateFactory<ID2D1Factory1>(Vortice.Direct2D1.FactoryType.SingleThreaded);
    private readonly IDWriteFactory5 write = DWrite.DWriteCreateFactory<IDWriteFactory5>();
    private readonly ID2D1Device device;
    private readonly ID2D1DeviceContext context;
    private readonly int owner = Environment.CurrentManagedThreadId;
    private readonly Dictionary<string, (IDWriteFontCollection1 Collection, string Family)> fonts = [];
    private readonly List<Rect> labels = [];
    private ID2D1Bitmap1? target;
    private ID2D1Bitmap1? staging;
    private int width, height;
    private bool drawing;
    public string Adapter { get; }
    public bool ClipOverflow { get; set; }
    public HashSet<string> Unsupported { get; } = [];
    public double FinishMs { get; private set; }
    public int TextDraws { get; private set; }

    public Direct2DCanvas()
    {
        using var dxgi = d3d.QueryInterface<IDXGIDevice>();
        using var adapter = dxgi.GetAdapter();
        Adapter = adapter.Description.Description;
        device = factory.CreateDevice(dxgi);
        context = device.CreateDeviceContext(DeviceContextOptions.None);
        context.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale;
    }

    private void CheckThread()
    {
        if (Environment.CurrentManagedThreadId != owner) throw new InvalidOperationException("Direct2D owning thread required.");
    }

    public void StartDrawing(double sizeX, double sizeY)
    {
        CheckThread();
        if (drawing) context.EndDraw().CheckError();
        drawing = false;
        width = (int)sizeX; height = (int)sizeY;
        if (target == null || target.PixelSize.Width != width || target.PixelSize.Height != height)
        {
            context.Target = null;
            target?.Dispose(); staging?.Dispose();
            var format = new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
            target = context.CreateBitmap(new SizeI(width, height), new BitmapProperties1(format, 96, 96, BitmapOptions.Target));
            staging = context.CreateBitmap(new SizeI(width, height), new BitmapProperties1(format, 96, 96, BitmapOptions.CpuRead | BitmapOptions.CannotDraw));
        }
        context.Target = target;
        context.Transform = Matrix3x2.Identity;
        context.BeginDraw(); drawing = true;
        context.Clear(new DColor(0, 0, 0, 0));
        Unsupported.Clear(); labels.Clear(); TextDraws = 0; FinishMs = 0;
    }

    public unsafe SKBitmap FinishDrawing()
    {
        CheckThread();
        var started = Stopwatch.GetTimestamp();
        if (!drawing) throw new InvalidOperationException("StartDrawing is required.");
        drawing = false;
        context.EndDraw().CheckError();
        if (Unsupported.Count != 0) throw new NotSupportedException(string.Join(", ", Unsupported));
        staging!.CopyFromBitmap(target!).CheckError();
        var mapped = staging.Map(MapOptions.Read);
        try
        {
            var output = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            for (var y = 0; y < height; y++)
                Buffer.MemoryCopy((byte*)mapped.Bits + y * mapped.Pitch, (byte*)output.GetPixels() + y * output.RowBytes,
                    output.RowBytes, width * 4L);
            return output;
        }
        finally { staging.Unmap(); FinishMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds; }
    }

    private static DColor Convert(MapColor c, double opacity = 1) => new(c.R / 255f, c.G / 255f, c.B / 255f,
        (byte)Math.Clamp(c.A * opacity, 0, 255) / 255f);
    private Rect Clip => new(-5, -5, width + 10, height + 10);
    public void DrawBackground(MapBrush style)
    {
        CheckThread();
        context.Clear(Convert(style.Paint.BackgroundColor));
    }
    private ID2D1PathGeometry1 PathGeometry(List<MapPoint> points, bool closed)
    {
        var path = factory.CreatePathGeometry();
        using var sink = path.Open();
        sink.SetFillMode(Vortice.Direct2D1.FillMode.Alternate);
        sink.BeginFigure(new Vector2((float)points[0].X, (float)points[0].Y), FigureBegin.Filled);
        for (var i = 1; i < points.Count; i++) sink.AddLine(new Vector2((float)points[i].X, (float)points[i].Y));
        sink.EndFigure(closed ? FigureEnd.Closed : FigureEnd.Open);
        sink.Close();
        return path;
    }

    public void DrawLineString(List<MapPoint> geometry, MapBrush style)
    {
        CheckThread();
        if (style.Paint.LineWidth <= 0) return;
        if (ClipOverflow) geometry = LineClipper.ClipPolyline(geometry, Clip);
        if (geometry == null || geometry.Count < 2) return;
        using var path = PathGeometry(geometry, false);
        using var brush = context.CreateSolidColorBrush(Convert(style.Paint.LineColor, style.Paint.LineOpacity));
        var cap = style.Paint.LineCap switch { PenLineCap.Round => CapStyle.Round, PenLineCap.Square => CapStyle.Square, _ => CapStyle.Flat };
        var props = new StrokeStyleProperties { StartCap = cap, EndCap = cap, DashCap = CapStyle.Flat,
            LineJoin = LineJoin.Miter, MiterLimit = 4, DashStyle = style.Paint.LineDashArray.Length == 0 ? DashStyle.Solid : DashStyle.Custom };
        // Direct2D custom dash lengths are multiples of stroke width; Skia receives pixels.
        var dashes = style.Paint.LineDashArray.Select(d => (float)(d / style.Paint.LineWidth)).ToArray();
        using var stroke = factory.CreateStrokeStyle(props, dashes);
        context.DrawGeometry(path, brush, (float)style.Paint.LineWidth, stroke);
    }

    public void DrawPolygon(List<MapPoint> geometry, MapBrush style)
    {
        CheckThread();
        if (ClipOverflow) { Unsupported.Add("overzoom polygon clipping"); return; }
        using var path = PathGeometry(geometry, false); // Match Skia's implicit fill closure.
        using var brush = context.CreateSolidColorBrush(Convert(style.Paint.FillColor, style.Paint.FillOpacity));
        context.FillGeometry(path, brush);
    }

    private IDWriteTextLayout Layout(MapBrush style, string text, float maxWidth)
    {
        var name = style.Paint.TextFont.First();
        var path = System.IO.Path.Combine(style.GlyphsDirectory ?? "", name + ".ttf");
        if (!File.Exists(path)) path = System.IO.Path.ChangeExtension(path, ".otf");
        if (!File.Exists(path)) throw new NotSupportedException("Exact local font unavailable: " + name);
        if (!fonts.TryGetValue(path, out var font))
        {
            using var file = write.CreateFontFileReference(path);
            using var builder = write.CreateFontSetBuilder();
            builder.AddFontFile(file);
            using var set = builder.CreateFontSet();
            var collection = write.CreateFontCollectionFromFontSet(set);
            using var family = collection.GetFontFamily(0);
            using var names = family.FamilyNames;
            font = (collection, names.GetString(0));
            fonts.Add(path, font);
        }
        using var format = write.CreateTextFormat(font.Family, font.Collection, FontWeight.Normal, FontStyle.Normal,
            FontStretch.Normal, (float)style.Paint.TextSize);
        format.WordWrapping = WordWrapping.NoWrap;
        return write.CreateTextLayout(text, format, maxWidth, height);
    }

    private static string Text(MapBrush style) => style.Paint.TextTransform switch
    {
        TextTransform.Uppercase => style.Text.ToUpper(), TextTransform.Lowercase => style.Text.ToLower(), _ => style.Text
    };

    public void DrawText(MapPoint geometry, MapBrush style)
    {
        CheckThread();
        try
        {
            var text = Text(style);
            using var layout = Layout(style, text, width * 4);
            if (text.Contains('\n') || layout.Metrics.Width > style.Paint.TextMaxWidth * style.Paint.TextSize)
            { Unsupported.Add("point label wrapping"); return; }
            var bounds = new Rect(geometry.X - layout.Metrics.Width / 2, geometry.Y - style.Paint.TextSize / 2,
                layout.Metrics.Width, style.Paint.TextSize);
            bounds.Inflate(5, 5);
            if ((ClipOverflow && !Clip.Contains(bounds)) || labels.Any(b => b.IntersectsWith(bounds))) return;
            labels.Add(bounds);
            var align = style.Paint.TextJustify switch { TextAlignment.Left => 0, TextAlignment.Right => layout.Metrics.Width, _ => layout.Metrics.Width / 2 };
            using var renderer = new GlyphRenderer(this, style, null);
            layout.Draw(renderer, (float)(geometry.X + style.Paint.TextOffset.X * style.Paint.TextSize) - align,
                (float)(geometry.Y + style.Paint.TextOffset.Y * style.Paint.TextSize + style.Paint.TextSize / 2) - layout.LineMetrics[0].Baseline);
            TextDraws++;
        }
        catch (NotSupportedException e) { Unsupported.Add(e.Message); }
    }

    public void DrawTextOnPath(List<MapPoint> geometry, MapBrush style)
    {
        CheckThread();
        try
        {
            geometry = LineClipper.ClipPolyline(geometry, Clip);
            if (geometry == null || geometry.Count < 2) return;
            if (geometry[0].X > geometry[^1].X || (geometry[0].X == geometry[^1].X && geometry[0].Y < geometry[^1].Y)) geometry.Reverse();
            double? previous = null;
            double length = 0;
            for (var i = 1; i < geometry.Count; i++)
            {
                var delta = geometry[i] - geometry[i - 1];
                if (delta.Length == 0) continue;
                var angle = Math.Atan2(delta.Y, delta.X);
                if (previous.HasValue && Math.Abs(Math.Atan2(Math.Sin(angle - previous.Value), Math.Cos(angle - previous.Value))) > Math.PI / 3) return;
                previous = angle; length += delta.Length;
            }
            if (style.Text.Length * style.Paint.TextSize * 0.2 >= length) return;
            var left = geometry.Min(p => p.X); var top = geometry.Min(p => p.Y);
            var bounds = new Rect(left, top, geometry.Max(p => p.X) - left, geometry.Max(p => p.Y) - top);
            bounds.Inflate(style.Paint.TextSize, style.Paint.TextSize);
            if (labels.Any(b => b.IntersectsWith(bounds))) return;
            labels.Add(bounds);
            using var layout = Layout(style, Text(style).Replace('\n', ' ').Replace('\r', ' '), width * 8);
            using var renderer = new GlyphRenderer(this, style, geometry);
            layout.Draw(renderer, (float)style.Paint.TextOffset.X, -layout.LineMetrics[0].Baseline);
            TextDraws++;
        }
        catch (NotSupportedException e) { Unsupported.Add(e.Message); }
    }

    private sealed class GlyphRenderer(Direct2DCanvas owner, MapBrush style, List<MapPoint>? road) : TextRendererBase
    {
        public override void DrawGlyphRun(IntPtr clientDrawingContext, float baselineOriginX, float baselineOriginY,
            Vortice.DCommon.MeasuringMode measuringMode, GlyphRun run, GlyphRunDescription description, IUnknown effect)
        {
            if (run.BidiLevel != 0 || run.IsSideways)
            { owner.Unsupported.Add("bidirectional/sideways labels"); return; }
            using var brush = owner.context.CreateSolidColorBrush(Convert(style.Paint.TextColor, style.Paint.TextOpacity));
            using var halo = owner.context.CreateSolidColorBrush(Convert(style.Paint.TextStrokeColor, style.Paint.TextOpacity));
            var indices = run.Indices ?? throw new InvalidOperationException("Missing glyph indices");
            var advances = run.Advances ?? throw new InvalidOperationException("Missing glyph advances");
            var face = run.FontFace ?? throw new InvalidOperationException("Missing glyph face");
            for (var i = 0; i < indices.Length; i++)
            {
                var advance = advances[i];
                var position = new Vector2(baselineOriginX, baselineOriginY);
                float angle = 0;
                if (road != null)
                {
                    var distance = baselineOriginX + advance / 2;
                    var found = false;
                    for (var j = 1; j < road.Count; j++)
                    {
                        var d = road[j] - road[j - 1];
                        if (d.Length == 0) continue;
                        if (distance <= d.Length && distance >= 0)
                        {
                            position = new Vector2((float)(road[j - 1].X + d.X * distance / d.Length),
                                (float)(road[j - 1].Y + d.Y * distance / d.Length));
                            angle = (float)Math.Atan2(d.Y, d.X); found = true; break;
                        }
                        distance -= (float)d.Length;
                    }
                    if (!found) { baselineOriginX += advance; continue; }
                }
                using var geometry = owner.factory.CreatePathGeometry();
                using (var sink = geometry.Open())
                {
                    face.GetGlyphRunOutline(run.FontEmSize, [indices[i]], [advance], [run.Offsets?[i] ?? default], false, false, sink);
                    sink.Close();
                }
                owner.context.Transform = road == null ? Matrix3x2.CreateTranslation(position)
                    : Matrix3x2.CreateTranslation(-advance / 2, (float)style.Paint.TextOffset.Y) * Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(position);
                if (style.Paint.TextStrokeWidth != 0) owner.context.DrawGeometry(geometry, halo, (float)style.Paint.TextStrokeWidth);
                owner.context.FillGeometry(geometry, brush);
                baselineOriginX += advance;
            }
            owner.context.Transform = Matrix3x2.Identity;
        }
    }

    public void DrawPoint(MapPoint geometry, MapBrush style)
    {
        CheckThread();
        // Symbol points without an icon are drawn in the separate text pass.
        if (!string.IsNullOrEmpty(style.Paint.IconImage)) Unsupported.Add("icons/points");
    }
    public void DrawImage(Stream imageStream, MapBrush style) => Unsupported.Add("raster images");
    public void DrawUnknown(List<List<MapPoint>> geometry, MapBrush style) => Unsupported.Add("unknown geometry");
    public void Dispose()
    {
        CheckThread();
        if (drawing) { context.EndDraw(); drawing = false; }
        context.Target = null;
        staging?.Dispose(); target?.Dispose();
        foreach (var font in fonts.Values) font.Collection.Dispose();
        context.Dispose(); device.Dispose(); factory.Dispose(); write.Dispose(); d3d.Dispose();
    }
}
