using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;
using WuGing.VectorTileRenderer;
using WuGing.VectorTileRenderer.Direct2DSpike;
using WuGing.VectorTileRenderer.GpuValidation;

var root = Path.GetFullPath(args.ElementAtOrDefault(0) ?? ".");
var output = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/direct2d-spike");
var iterations = int.Parse(args.ElementAtOrDefault(2) ?? "30");
if (iterations < 5) throw new ArgumentException("At least five measured iterations required.");
Directory.CreateDirectory(output);
List<object> results = [];
void Record(object value) { results.Add(value); Console.WriteLine(JsonSerializer.Serialize(value)); }
void Require(bool condition, string name)
{
    Record(new { Case = "Check", Name = name, Passed = condition });
    if (!condition) throw new InvalidOperationException(name);
}
try
{
    var startup = Stopwatch.GetTimestamp();
    using var d2d = new Direct2DCanvas();
    Record(new { Case = "Environment", d2d.Adapter, Direct2DStartupMs = Stopwatch.GetElapsedTime(startup).TotalMilliseconds,
        Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription, Iterations = iterations,
        D3DDriverType = "Hardware (no WARP fallback)", Vortice = "3.8.3", Thread = Environment.CurrentManagedThreadId });
    using var pump = new RenderThread();
    using var gl = new WindowsGlContext();
    using var context = GRContext.CreateGl() ?? throw new NotSupportedException("Skia GPU context unavailable");
    Require(gl.IsHardwarePixelFormat, "Skia baseline uses hardware GL");
    using var cpu = new ProbeCanvas(null);
    using var gpu = new ProbeCanvas(context) { VerifyReadback = true };
    d2d.StartDrawing(64, 64);
    d2d.DrawBackground(new Brush { Paint = new Paint { BackgroundColor = Color.FromArgb(255, 20, 40, 60) } });
    d2d.DrawPolygon([new(16, 16), new(48, 16), new(48, 48), new(16, 48)],
        new Brush { Paint = new Paint { FillColor = Color.FromArgb(255, 200, 100, 50) } });
    using (var check = d2d.FinishDrawing())
    {
        Require(check.GetPixel(32, 32) == new SKColor(200, 100, 50) && check.GetPixel(2, 2) == new SKColor(20, 40, 60),
            "Direct2D readback has expected geometry, channels and orientation");
    }

    var fontDirectory = Path.Combine(root, "styles/fonts");
    foreach (var vertical in new[] { false, true })
    {
        List<Point> road = vertical ? [new(100, 220), new(100, 30)] : [new(20, 100), new(120, 95), new(240, 100)];
        var original = road.ToArray();
        var text = new Brush { Text = "WEST 31ST AVE", GlyphsDirectory = fontDirectory,
            Paint = new Paint { TextFont = ["OpenSans Regular"], TextSize = 16, TextColor = Color.FromArgb(255, 0, 0, 0),
                TextStrokeWidth = 1, TextStrokeColor = Color.FromArgb(255, 255, 255, 255) } };
        SKBitmap Label(ICanvas canvas, List<Point> points)
        {
            canvas.StartDrawing(256, 256);
            canvas.DrawBackground(new Brush { Paint = new Paint { BackgroundColor = Color.FromArgb(255, 235, 235, 235) } });
            canvas.DrawTextOnPath(points, text);
            return canvas.FinishDrawing();
        }
        using var label = Label(d2d, road);
        using var reverse = Label(d2d, road.AsEnumerable().Reverse().ToList());
        Require(label.Bytes.SequenceEqual(reverse.Bytes) && road.SequenceEqual(original), "DirectWrite label direction and input ownership " + vertical);
        Require(label.Pixels.Count(p => p.Red < 100) > 100, "DirectWrite label is visible " + vertical);
        using var skia = Label(cpu, road);
        PixelComparison.Save(label, Path.Combine(output, $"path-label-vertical{vertical}-direct2d.png"));
        PixelComparison.Save(skia, Path.Combine(output, $"path-label-vertical{vertical}-skia.png"));
        Record(new { Case = "LabelComparison", Vertical = vertical, Comparison = PixelComparison.Compare(skia, label,
            Path.Combine(output, $"path-label-vertical{vertical}-diff.png")) });
    }

    var workloads = new List<Workload> { Workload.Synthetic(output, fontDirectory, false), Workload.Synthetic(output, fontDirectory, true), Workload.Zurich(root, true) };
    using var local = args.Length > 3 ? new MbTilesWorkloads(Path.GetFullPath(args[3]), root) : null;
    if (local != null)
    {
        foreach (var evidence in local.Evidence) Record(evidence);
        workloads.Add(local.Workloads.Where(w => w.Name.EndsWith("warm-source")).MaxBy(w => w.Zoom)!);
    }

    foreach (var workload in workloads)
    foreach (var size in new[] { 256, 512 })
    {
        SKBitmap Render(ICanvas canvas, List<string>? filter) => pump.Run(() => Renderer.Render(workload.Style, canvas,
            workload.X, workload.Y, workload.Zoom, size, size, 1, filter));
        try
        {
            using var complete = Render(d2d, null);
            using var reference = Render(cpu, null);
            PixelComparison.Save(complete, Path.Combine(output, $"{workload.Name}-{size}-full-direct2d.png"));
            PixelComparison.Save(reference, Path.Combine(output, $"{workload.Name}-{size}-full-skia.png"));
            Record(new { Case = "FullCompatibility", workload.Name, Size = size, Completed = true, d2d.TextDraws,
                Comparison = PixelComparison.Compare(reference, complete, Path.Combine(output, $"{workload.Name}-{size}-full-diff.png")),
                Interpretation = "Diagnostic comparison; not full map/style conformance" });
        }
        catch (NotSupportedException error)
        {
            Record(new { Case = "FullCompatibility", workload.Name, Size = size, Completed = false, Reason = error.Message });
        }

        var geometryLayers = workload.Style.Layers.Where(l => l.Type is "fill" or "line").Select(l => l.SourceLayer).Distinct().ToList();
        using var cpuImage = Render(cpu, geometryLayers);
        using var gpuImage = Render(gpu, geometryLayers);
        Require(gpu.TextureBacked && gpu.ReadbackSucceeded == true, "Skia checked GPU readback " + workload.Name + size);
        using var d2dImage = Render(d2d, geometryLayers);
        using var repeat = Render(d2d, geometryLayers);
        Require(d2dImage.Bytes.SequenceEqual(repeat.Bytes), "Direct2D deterministic geometry " + workload.Name + size);
        Require(d2dImage.Pixels.Distinct().Take(3).Count() >= 3, "Direct2D nonblank geometry " + workload.Name + size);
        foreach (var (name, image) in new[] { ("cpu", cpuImage), ("gpu", gpuImage), ("direct2d", d2dImage) })
            PixelComparison.Save(image, Path.Combine(output, $"{workload.Name}-{size}-geometry-{name}.png"));
        var geometryComparison = PixelComparison.Compare(cpuImage, d2dImage, Path.Combine(output, $"{workload.Name}-{size}-geometry-diff.png"));
        Record(new { Case = "GeometryComparison", workload.Name, Size = size, Comparison = geometryComparison });
        if (!geometryComparison.Passed)
        {
            Record(new { Case = "TimingSkipped", workload.Name, Size = size, Reason = "Direct2D geometry image gate failed" });
            continue;
        }
        gpu.VerifyReadback = false;
        var canvases = new[] { (Name: "SkiaCPU", Canvas: (ICanvas)cpu), (Name: "SkiaGPU", Canvas: (ICanvas)gpu), (Name: "Direct2D", Canvas: (ICanvas)d2d) };
        var samples = canvases.ToDictionary(c => c.Name, _ => new List<(double Render, double Total, double Finish)>());
        for (var iteration = -5; iteration < iterations; iteration++)
        for (var index = 0; index < canvases.Length; index++)
        {
            var entry = canvases[(index + iteration + 6) % canvases.Length];
            var started = Stopwatch.GetTimestamp();
            using var image = Render(entry.Canvas, geometryLayers);
            var renderMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            PixelComparison.Save(image, Path.Combine(output, $"timing-{entry.Name}.png"));
            var totalMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (iteration >= 0) samples[entry.Name].Add((renderMs, totalMs, entry.Name == "Direct2D" ? d2d.FinishMs : entry.Name == "SkiaGPU" ? gpu.ReadbackMs : 0));
        }
        foreach (var entry in samples)
        {
            double Quantile(IEnumerable<double> values, double q) { var a = values.Order().ToArray(); return a[(int)Math.Ceiling(q * a.Length) - 1]; }
            Record(new { Case = "GeometryTiming", workload.Name, Size = size, Backend = entry.Key,
                RenderMedianMs = Quantile(entry.Value.Select(s => s.Render), 0.5), TotalMedianMs = Quantile(entry.Value.Select(s => s.Total), 0.5),
                TotalP95Ms = Quantile(entry.Value.Select(s => s.Total), 0.95), FinishMedianMs = Quantile(entry.Value.Select(s => s.Finish), 0.5),
                Samples = entry.Value.Select(s => new { RenderMs = s.Render, TotalMs = s.Total, FinishMs = s.Finish }).ToArray(),
                Scope = "Geometry-only; warm provider/host; PNG write-close included; not full-map performance" });
        }
        gpu.VerifyReadback = true;
    }
    Record(new { Case = "Completed", Meaning = "Spike executed; production adoption is not implied" });
    return 0;
}
catch (Exception error)
{
    Record(new { Case = "Failure", Error = error.ToString() });
    return 1;
}
finally
{
    File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { TimestampUtc = DateTime.UtcNow, Results = results },
        new JsonSerializerOptions { WriteIndented = true }));
}
