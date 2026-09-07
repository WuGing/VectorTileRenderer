using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;

namespace WuGing.VectorTileRenderer.GpuValidation;

// Diagnostic experiments only: no production surface or acceptance-policy changes.
internal static class ParityInvestigation
{
    public static int Run(string[] args)
    {
        var root = Path.GetFullPath(args.ElementAtOrDefault(0) ?? ".");
        var output = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/parity-investigation");
        Directory.CreateDirectory(output);
        var records = new List<object>();
        void Record(object value)
        {
            records.Add(value);
            Console.WriteLine(JsonSerializer.Serialize(value));
        }
        try
        {
            using var host = new WindowsGlContext();
            if (!host.IsHardwarePixelFormat) throw new NotSupportedException("Hardware GL required.");
            using var context = GRContext.CreateGl() ?? throw new NotSupportedException("Skia GL unavailable.");
            using var pump = new RenderThread();
            Record(new { Case = "Environment", host.Vendor, host.Renderer, host.Version,
                Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
                Skia = typeof(SKBitmap).Assembly.GetName().Version?.ToString() });
            var synthetic = Workload.Synthetic(output, Path.Combine(root, "styles/fonts"), false);
            using var local = args.Length > 2 ? new MbTilesWorkloads(Path.GetFullPath(args[2]), root) : null;
            var workloads = new List<Workload> { synthetic };
            if (local != null)
            {
                foreach (var evidence in local.Evidence) Record(evidence);
                workloads.Add(local.Workloads.Where(w => w.Name.EndsWith("warm-source")).MaxBy(w => w.Zoom)!);
            }
            foreach (var workload in workloads)
            {
                var groups = workload.Style.Layers.Select(l => l.SourceLayer)
                    .Where(l => !string.IsNullOrEmpty(l)).Distinct().ToList();
                foreach (var group in new[] { "all" }.Concat(groups))
                {
                    List<string>? filter = group == "all" ? null : [group];
                    using var capture = new PictureCanvas();
                    pump.Run(() => Renderer.Render(workload.Style, capture, workload.X, workload.Y,
                        workload.Zoom, 256, 256, 1, filter));
                    var picture = capture.Picture!;
                    var name = $"{workload.Name}-{group}";
                    using var cpu = Replay(picture, null, 0);
                    using var productionCpu = new ProbeCanvas(null);
                    using var productionGpu = new ProbeCanvas(context) { VerifyReadback = true };
                    var reference = pump.Run(() => Renderer.Render(workload.Style, productionCpu,
                        workload.X, workload.Y, workload.Zoom, 256, 256, 1, filter));
                    var gpuReference = pump.Run(() => Renderer.Render(workload.Style, productionGpu,
                        workload.X, workload.Y, workload.Zoom, 256, 256, 1, filter));
                    Record(new { Case = "Capture", Name = name, CpuMatchesProduction = cpu.Bytes.SequenceEqual(reference.Bytes),
                        GpuChecked = productionGpu.IsGpuEnabled && productionGpu.TextureBacked && productionGpu.ReadbackSucceeded == true });
                    if (!cpu.Bytes.SequenceEqual(reference.Bytes) || !productionGpu.TextureBacked || productionGpu.ReadbackSucceeded != true)
                        throw new InvalidOperationException("Capture/reference validation failed.");
                    PixelComparison.Save(cpu, Path.Combine(output, name + "-cpu.png"));
                    foreach (var samples in new[] { 0, 4, 8 })
                    {
                        using var gpu = Replay(picture, context, samples);
                        var comparison = PixelComparison.Compare(cpu, gpu, Path.Combine(output, name + $"-s{samples}-diff.png"));
                        PixelComparison.Save(gpu, Path.Combine(output, name + $"-s{samples}-gpu.png"));
                        using var repeat = Replay(picture, context, samples);
                        if (!gpu.Bytes.SequenceEqual(repeat.Bytes) || (samples == 0 && !gpu.Bytes.SequenceEqual(gpuReference.Bytes)))
                            throw new InvalidOperationException("Replay is nondeterministic or differs from production.");
                        Record(new { Case = "LayerReplay", Name = name, RequestedSamples = samples, comparison,
                            GpuMatchesProduction = gpu.Bytes.SequenceEqual(gpuReference.Bytes),
                            RepeatExact = gpu.Bytes.SequenceEqual(repeat.Bytes), Metrics = Measure(cpu, gpu) });
                    }
                }
            }

            foreach (var aa in new[] { false, true })
            foreach (var width in new[] { 1f, 2f, 4f })
            foreach (var outline in new[] { false, true })
            {
                using var recorder = new SKPictureRecorder();
                var canvas = recorder.BeginRecording(new SKRect(0, 0, 256, 256));
                canvas.Clear(new SKColor(232, 238, 242));
                using var paint = new SKPaint { IsAntialias = aa, Style = SKPaintStyle.Stroke,
                    StrokeWidth = width, StrokeCap = SKStrokeCap.Round, Color = new SKColor(168, 79, 54) };
                for (var i = 0; i < 40; i++)
                {
                    var y = (float)(0.08 + i * 0.84 / 40) * 256;
                    using var path = new SKPath();
                    path.MoveTo(0.04f * 256, y);
                    path.LineTo(0.3f * 256, y + 0.025f * 256);
                    path.LineTo(0.65f * 256, y - 0.025f * 256);
                    path.LineTo(0.96f * 256, y);
                    if (outline)
                    {
                        using var filled = paint.GetFillPath(path);
                        using var fillPaint = new SKPaint { IsAntialias = aa, Color = paint.Color, Style = SKPaintStyle.Fill };
                        canvas.DrawPath(filled, fillPaint);
                    }
                    else
                    {
                        canvas.DrawPath(path, paint);
                    }
                }
                using var picture = recorder.EndRecording();
                using var cpu = Replay(picture, null, 0);
                using var gpu = Replay(picture, context, 0);
                var name = $"raw-strokes-aa{aa}-w{width}-outline{outline}";
                var comparison = PixelComparison.Compare(cpu, gpu, Path.Combine(output, name + "-diff.png"));
                Record(new { Case = "RawSkia", AA = aa, Width = width, Outline = outline, comparison, Metrics = Measure(cpu, gpu) });
            }

            foreach (var alpha in new byte[] { 255, 204 })
            {
                using var recorder = new SKPictureRecorder();
                var canvas = recorder.BeginRecording(new SKRect(0, 0, 256, 256));
                canvas.Clear(new SKColor(232, 238, 242));
                using var paint = new SKPaint { Color = new SKColor(114, 171, 130, alpha), IsAntialias = true };
                canvas.DrawRect(32, 32, 192, 192, paint);
                using var picture = recorder.EndRecording();
                using var cpu = Replay(picture, null, 0);
                using var gpu = Replay(picture, context, 0);
                Record(new { Case = "SolidRectangle", Alpha = alpha, CpuInterior = cpu.GetPixel(100, 100).ToString(),
                    GpuInterior = gpu.GetPixel(100, 100).ToString(), Metrics = Measure(cpu, gpu) });
            }
            return 0; // Experiment completed, not acceptance of the original image gates.
        }
        catch (Exception error)
        {
            Record(new { Case = "Failure", Error = error.ToString() });
            return 1;
        }
        finally
        {
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(
                new { TimestampUtc = DateTime.UtcNow, Results = records }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static SKBitmap Replay(SKPicture picture, GRContext? context, int samples)
    {
        var info = new SKImageInfo(256, 256, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = context == null ? SKSurface.Create(info) : SKSurface.Create(context, true, info, samples);
        if (surface == null) throw new InvalidOperationException($"Surface unavailable: samples={samples}");
        surface.Canvas.DrawPicture(picture);
        surface.Canvas.Flush();
        using var image = surface.Snapshot();
        if (context != null && !image.IsTextureBacked) throw new InvalidOperationException("Replay used CPU fallback.");
        var bitmap = new SKBitmap(info);
        if (!image.ReadPixels(info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0))
        {
            bitmap.Dispose();
            throw new InvalidOperationException("Replay readback failed.");
        }
        return bitmap;
    }

    private static object Measure(SKBitmap cpu, SKBitmap gpu)
    {
        var changed = 0;
        var interiorChanged = 0;
        var alphaChanged = 0;
        var maximum = 0;
        var interiorOverOne = 0;
        var deltas = new Dictionary<string, int>();
        for (var y = 0; y < cpu.Height; y++)
        for (var x = 0; x < cpu.Width; x++)
        {
            var a = cpu.GetPixel(x, y);
            var b = gpu.GetPixel(x, y);
            if (a == b) continue;
            changed++;
            var delta = $"{b.Red - a.Red},{b.Green - a.Green},{b.Blue - a.Blue},{b.Alpha - a.Alpha}";
            deltas[delta] = deltas.GetValueOrDefault(delta) + 1;
            if (a.Alpha != b.Alpha) alphaChanged++;
            maximum = Math.Max(maximum, new[] { Math.Abs(a.Red - b.Red), Math.Abs(a.Green - b.Green),
                Math.Abs(a.Blue - b.Blue), Math.Abs(a.Alpha - b.Alpha) }.Max());
            // CPU-flat 3x3 neighborhoods distinguish interiors from AA edges.
            if (x > 0 && y > 0 && x < cpu.Width - 1 && y < cpu.Height - 1)
            {
                var flat = true;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++) flat &= cpu.GetPixel(x + dx, y + dy) == a;
                if (flat)
                {
                    interiorChanged++;
                    if (Math.Abs(a.Red - b.Red) > 1 || Math.Abs(a.Green - b.Green) > 1 || Math.Abs(a.Blue - b.Blue) > 1)
                        interiorOverOne++;
                }
            }
        }
        return new { ChangedPixels = changed, CpuFlatInteriorChangedPixels = interiorChanged, AlphaChangedPixels = alphaChanged,
            MaximumChannelError = maximum, CpuFlatInteriorOverOne = interiorOverOne,
            CommonChangedDeltas = deltas.OrderByDescending(p => p.Value).Take(5).ToDictionary() };
    }

    private sealed class PictureCanvas : SkiaCanvas, IDisposable
    {
        private readonly SKPictureRecorder recorder = new();
        public SKPicture? Picture { get; private set; }
        public override void StartDrawing(double width, double height)
        {
            base.StartDrawing(width, height);
            canvas = recorder.BeginRecording(new SKRect(0, 0, (float)width, (float)height));
        }
        protected override void OnBeforeFinishDrawing() => Picture = recorder.EndRecording();
        protected override void Dispose(bool disposing)
        {
            Picture?.Dispose();
            recorder.Dispose();
            base.Dispose(disposing);
        }
    }
}
