using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;

namespace WuGing.VectorTileRenderer.GpuValidation;

internal static class Program
{
    private static readonly List<object> results = [];

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--label-timing")
        {
            return LabelTiming.Run(args.Skip(1).ToArray());
        }
        if (args.FirstOrDefault() == "--labels")
        {
            return LabelValidation.Run(args.Skip(1).ToArray());
        }
        if (args.FirstOrDefault() == "--parity")
        {
            return ParityInvestigation.Run(args.Skip(1).ToArray());
        }
        var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        var output = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(root, "artifacts", "gpu-validation"));
        var iterations = args.Length > 2 ? int.Parse(args[2]) : 30;
        if (iterations < 5) throw new ArgumentException("Use at least 5 measured iterations.");
        Directory.CreateDirectory(output);
        var exitCode = 1;
        try
        {
            Record(new { Case = "Environment", Runtime = RuntimeInformation.FrameworkDescription,
                OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                SkiaSharp = typeof(SKBitmap).Assembly.GetName().Version?.ToString(),
                LogicalProcessors = Environment.ProcessorCount, OwnerThread = Environment.CurrentManagedThreadId,
                Iterations = iterations, MinimumUsefulSpeedup = 1.10, Concurrency = 1,
                CachePolicy = "OS file cache uncontrolled; decoded source reused; image hit measured separately" });
            Require(!WindowsGlContext.HasCurrentContext, "Initial thread has no native context");
            foreach (var backend in new[] { RenderBackend.Cpu, RenderBackend.Gpu, RenderBackend.Auto })
            {
                var selected = CanvasFactory.Create(backend);
                Require(selected is not SkiaGpuCanvas { IsGpuEnabled: true }, $"{backend} without context selects CPU");
            }

            var fonts = Path.Combine(root, "styles", "fonts");
            var geometry = Workload.Synthetic(output, fonts, false);
            var text = Workload.Synthetic(output, fonts, true);
            using var localDataset = args.Length > 3 ? new MbTilesWorkloads(Path.GetFullPath(args[3]), root) : null;
            if (localDataset != null)
            {
                foreach (var evidence in localDataset.Evidence) Record(evidence);
            }
            using var pump = new RenderThread();
            using (var fallback = new ProbeCanvas(null))
            {
                using var bitmap = pump.Run(() => Renderer.Render(geometry.Style, fallback, 0, 0, 14, 256, 256));
                Require(!fallback.IsGpuEnabled && bitmap.GetPixel(0, 0).Alpha == 255, "Null-context canvas renders CPU pixels");
            }

            var startup = Stopwatch.GetTimestamp();
            using (var host = new WindowsGlContext())
            {
                Record(new { Case = "WGL", host.Vendor, host.Renderer, host.Version, host.IsHardwarePixelFormat,
                    StartupMs = Stopwatch.GetElapsedTime(startup).TotalMilliseconds });
                if (!host.IsHardwarePixelFormat)
                {
                    throw new NotSupportedException("Only a software GL pixel format is available; hardware GPU is unvalidated.");
                }
                startup = Stopwatch.GetTimestamp();
                using var context = GRContext.CreateGl() ?? throw new NotSupportedException("Skia could not wrap the current WGL context.");
                Record(new { Case = "SkiaContext", StartupMs = Stopwatch.GetElapsedTime(startup).TotalMilliseconds });
                Require(GpuFailureChecks.Run((name, passed) =>
                    Record(new { Case = "GpuFailureRegression", Name = name, Passed = passed })),
                    "GPU readback and Auto regressions");

                using var cpu = new ProbeCanvas(null);
                using var gpu = new ProbeCanvas(context) { VerifyReadback = true };
                var cpuGuard = new ThreadBoundCanvas(cpu, null);
                var gpuGuard = new ThreadBoundCanvas(gpu, host);
                var workloads = localDataset?.Workloads.ToArray()
                    ?? [geometry, text, Workload.Zurich(root, true), Workload.Zurich(root, false)];
                var parityPassed = true;
                var passedCases = new HashSet<(string Workload, int Size)>();
                foreach (var workload in workloads)
                {
                    foreach (var size in new[] { 256, 512, 1024 })
                    {
                        using var cpuImage = Render(pump, workload, cpuGuard, size);
                        using var gpuImage = Render(pump, workload, gpuGuard, size);
                        var name = $"{workload.Name}-{size}";
                        PixelComparison.Save(cpuImage, Path.Combine(output, name + "-cpu.png"));
                        PixelComparison.Save(gpuImage, Path.Combine(output, name + "-gpu.png"));
                        var comparison = PixelComparison.Compare(cpuImage, gpuImage, Path.Combine(output, name + "-diff.png"));
                        Require(gpu.IsGpuEnabled && gpu.TextureBacked && gpu.ReadbackSucceeded == true, name + " actual GPU and checked readback");
                        parityPassed &= comparison.Passed;
                        if (comparison.Passed) passedCases.Add((workload.Name, size));
                        Record(new { Case = "PixelParity", Workload = workload.Name, Size = size, comparison.MeanChannelError,
                            comparison.PixelsOver32Percent, comparison.Passed });
                    }
                }

                geometry.Style.SetSourceProvider("tiles", new Workload.YieldingSource(geometry.Source));
                using (var image = Render(pump, geometry, gpuGuard, 256))
                {
                    Require(gpuGuard.FinishThreadId == Environment.CurrentManagedThreadId && gpu.ReadbackSucceeded == true,
                        "Yielding provider returns to host render thread with valid readback");
                }
                try
                {
                    // No pump: guard detects the invalid continuation before native calls.
                    using var unexpected = Renderer.Render(geometry.Style, gpuGuard, 0, 0, 14, 256, 256).GetAwaiter().GetResult();
                    throw new InvalidOperationException("Expected wrong-thread continuation to be rejected.");
                }
                catch (InvalidOperationException error) when (error.Message.Contains("owning thread"))
                {
                    Record(new { Case = "UnhostedAsync", Result = "Guard rejected wrong-thread native access", gpuGuard.RejectedCalls });
                }
                geometry.Style.SetSourceProvider("tiles", geometry.Source);
                gpu.VerifyReadback = false;

                var memoryBefore = SettledPrivateBytes();
                foreach (var workload in workloads)
                {
                    foreach (var size in new[] { 256, 512, 1024 })
                    {
                        if (passedCases.Contains((workload.Name, size)))
                        {
                            Benchmark(pump, workload, cpuGuard, gpuGuard, gpu, size, output, iterations);
                        }
                        else
                        {
                            Record(new { Case = "PerformanceSkipped", Workload = workload.Name, Size = size,
                                Reason = "Declared pixel parity gate failed; tolerance unchanged" });
                        }
                    }
                }
                Record(new { Case = "RepeatedRenderMemory", BeforePrivateBytes = memoryBefore,
                    AfterPrivateBytes = SettledPrivateBytes(),
                    Interpretation = "Observation only; managed finalization forced, no native-memory plateau claim" });

                exitCode = parityPassed ? 0 : 1;
            }
            Require(!WindowsGlContext.HasCurrentContext, "Host shutdown unbound GL context");
            for (var i = 0; i < 3; i++)
            {
                using var recreated = new WindowsGlContext();
                using var context = GRContext.CreateGl() ?? throw new InvalidOperationException("Recreation failed");
                using var canvas = new ProbeCanvas(context) { VerifyReadback = true };
                using var image = Render(pump, geometry, new ThreadBoundCanvas(canvas, recreated), 256);
                Require(canvas.IsGpuEnabled && canvas.ReadbackSucceeded == true, $"Host recreation {i + 1}");
            }
        }
        catch (NotSupportedException error)
        {
            Record(new { Case = "Unavailable", error.Message });
            exitCode = 2;
        }
        catch (Exception error)
        {
            exitCode = 1;
            Record(new { Case = "Failure", Error = error.ToString() });
        }
        finally
        {
            Renderer.ProfileSink = null;
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(
                new { TimestampUtc = DateTime.UtcNow, ExitCode = exitCode, Results = results }, new JsonSerializerOptions { WriteIndented = true }));
        }
        return exitCode;
    }

    private static SKBitmap Render(RenderThread pump, Workload workload, ICanvas canvas, int size) =>
        pump.Run(() => Renderer.Render(workload.Style, canvas, workload.X, workload.Y, workload.Zoom, size, size));

    private static void Benchmark(RenderThread pump, Workload workload, ThreadBoundCanvas cpu, ThreadBoundCanvas gpu,
        ProbeCanvas gpuProbe, int size, string output, int iterations)
    {
        var samples = new Dictionary<string, List<Sample>> { ["CPU"] = [], ["GPU"] = [] };
        Renderer.RenderProfile? profile = null;
        Renderer.ProfileSink = value => profile = value;
        var cachePath = Path.Combine(output, "benchmark-cache.png");
        for (var iteration = -5; iteration < iterations; iteration++)
        {
            // Alternate order to reduce warmup/thermal/order bias. File reads are OS-cache-warm.
            foreach (var backend in iteration % 2 == 0 ? new[] { "CPU", "GPU" } : new[] { "GPU", "CPU" })
            {
                var canvas = backend == "GPU" ? gpu : cpu;
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var started = Stopwatch.GetTimestamp();
                using var bitmap = Render(pump, workload, canvas, size);
                var renderMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                if (backend == "GPU") RequireGpu(gpuProbe);
                var encodeStart = Stopwatch.GetTimestamp();
                PixelComparison.Save(bitmap, cachePath);
                var totalMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var encodeMs = Stopwatch.GetElapsedTime(encodeStart).TotalMilliseconds;
                var managedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                var hitStart = Stopwatch.GetTimestamp();
                using var hit = SKBitmap.Decode(cachePath) ?? throw new InvalidOperationException("PNG cache decode failed");
                var hitMs = Stopwatch.GetElapsedTime(hitStart).TotalMilliseconds;
                if (iteration >= 0)
                {
                    var measured = profile ?? throw new InvalidOperationException("Missing render profile");
                    samples[backend].Add(new Sample(renderMs, totalMs, measured.TileFetchDecodeMs,
                        measured.BuildStyleEvalMs, measured.DrawGeometryMs, Math.Max(0, measured.DrawTextMs - canvas.FinishMs),
                        canvas.FinishMs, backend == "GPU" ? gpuProbe.ReadbackMs : 0, encodeMs, hitMs, managedBytes));
                }
            }
        }
        Renderer.ProfileSink = null;
        foreach (var (backend, values) in samples)
        {
            Record(new { Case = "Timing", Workload = workload.Name, Size = size, Backend = backend,
                RenderMedianMs = Percentile(values.Select(s => s.RenderMs), 0.5),
                TotalMedianMs = Percentile(values.Select(s => s.TotalMs), 0.5),
                TotalP95Ms = Percentile(values.Select(s => s.TotalMs), 0.95), Samples = values }, quiet: true);
            Console.WriteLine($"Timing complete: {workload.Name}, {size}, {backend}");
        }
    }

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.Order().ToArray();
        return sorted[Math.Clamp((int)Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1)];
    }

    private static void RequireGpu(ProbeCanvas canvas)
    {
        if (!canvas.IsGpuEnabled) throw new InvalidOperationException("GPU timing silently fell back to CPU.");
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        Record(new { Case = "Check", Name = name, Passed = true });
    }

    private static long SettledPrivateBytes()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var process = Process.GetCurrentProcess();
        return process.PrivateMemorySize64;
    }

    private static void Record(object value, bool quiet = false)
    {
        results.Add(value);
        if (!quiet) Console.WriteLine(JsonSerializer.Serialize(value));
    }

    private sealed record Sample(double RenderMs, double TotalMs, double FetchDecodeMs, double StyleMs,
        double GeometryMs, double TextMs, double FinishMs, double ReadbackMs, double EncodeWriteMs,
        double ImageCacheHitMs, long ManagedBytes);
}
