using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace WuGing.VectorTileRenderer.GpuValidation;

internal static class LabelTiming
{
    public static int Run(string[] args)
    {
        var root = Path.GetFullPath(args[0]);
        var output = Path.GetFullPath(args[1]);
        var iterations = args.Length > 3 ? int.Parse(args[3]) : 30;
        if (iterations < 10) throw new ArgumentException("Use at least ten iterations.");
        Directory.CreateDirectory(output);
        using var workloads = new MbTilesWorkloads(Path.GetFullPath(args[2]), root);
        using var pump = new RenderThread();
        using var canvas = new ProbeCanvas(null);
        var results = new List<object>();
        foreach (var workload in workloads.Workloads.Where(w => args.Length < 5 || w.Name.Contains(args[4])))
        foreach (var size in args.Length > 5 ? new[] { int.Parse(args[5]) } : new[] { 256, 512 })
        {
            var samples = new List<double>();
            var textSamples = new List<double>();
            var decodeSamples = new List<double>();
            Renderer.RenderProfile? profile = null;
            Renderer.ProfileSink = value => profile = value;
            for (var i = -5; i < iterations; i++)
            {
                var start = Stopwatch.GetTimestamp();
                var tile = pump.Run(() => Renderer.Render(workload.Style, canvas, workload.X, workload.Y, workload.Zoom, size, size));
                if (tile == null) throw new InvalidOperationException("Missing benchmark tile.");
                var ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                if (i >= 0)
                {
                    samples.Add(ms);
                    if (profile == null) throw new InvalidOperationException("Missing render profile.");
                    textSamples.Add(profile.DrawTextMs);
                    decodeSamples.Add(profile.TileFetchDecodeMs);
                }
                if (i == iterations - 1) PixelComparison.Save(tile, Path.Combine(output, workload.Name + "-" + size + ".png"));
            }
            var sorted = samples.Order().ToArray();
            results.Add(new { workload.Name, Size = size, MedianMs = sorted[sorted.Length / 2], SamplesMs = samples, TextMs = textSamples, DecodeMs = decodeSamples });
            Console.WriteLine($"{workload.Name} {size}: {sorted[sorted.Length / 2]:F2} ms");
        }
        Renderer.ProfileSink = null;
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new
        {
            Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
            Cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"), Backend = "CPU",
            Cache = "No disk image cache; warm/fresh decoded providers; OS cache uncontrolled; 5 warmups per case",
            Iterations = iterations, workloads.Evidence, Results = results
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
