using System.Linq;
using System.Diagnostics;
using System.Text.Json;
using SkiaSharp;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Benchmarks;

internal static class SubmissionCensus
{
    public static void Time(string root, string output, int iterations)
    {
        if (iterations < 10) throw new ArgumentOutOfRangeException(nameof(iterations));
        using var source = new SingleMbTilesSource(Path.Combine(root, "tiles", "zurich.mbtiles"));
        var style = new Style(Path.Combine(root, "styles", "bright-style.json"))
        {
            FontDirectory = Path.Combine(root, "styles", "fonts")
        };
        style.SetSourceProvider(0, source);
        using var recording = new CountingCanvas();
        using var full = Renderer.Render(style, recording, 8580, 10646, 14, 512, 512).GetAwaiter().GetResult();
        using var reference = Replay();
        var pixels = reference.Pixels;
        var warmup = Stopwatch.StartNew();
        do
        {
            using var bitmap = Replay();
        }
        while (warmup.Elapsed < TimeSpan.FromSeconds(1));
        var samples = new List<object>();
        for (var i = 0; i < iterations; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            using var bitmap = Replay();
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (!bitmap.Pixels.SequenceEqual(pixels)) throw new InvalidOperationException("Replay pixels differ.");
            samples.Add(new { Iteration = i, ElapsedMs = elapsed, ManagedBytes = allocated });
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            LibrarySha256 = Hash(File.ReadAllBytes(typeof(Style).Assembly.Location)),
            PixelSha256 = Hash(reference.Bytes), Iterations = iterations, MinimumWarmupMs = 1000,
            Submissions = recording.Commands.Count, UniqueGeometry = recording.Counts.Count,
            Policy = "Replay real Bright z14 512px line/polygon commands in original order on fresh CPU canvases. Includes surface setup, native path construction/submission, FinishDrawing and canvas disposal. Excludes source decode, preparation, point/text drawing and PNG encoding. Exact replay pixels checked outside timers. No forced GC; managed counters exclude native memory.",
            Samples = samples
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(output);

        SKBitmap Replay()
        {
            using var canvas = new SkiaCanvas();
            canvas.StartDrawing(512, 512);
            canvas.DrawBackground(new Brush { Paint = new Paint { BackgroundColor = Color.FromRgb(255, 255, 255) } });
            foreach (var command in recording.Commands)
            {
                if (command.Polygon) canvas.DrawPolygon(command.Geometry, command.Brush);
                else canvas.DrawLineString(command.Geometry, command.Brush);
            }
            return canvas.FinishDrawing();
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));

    public static void Run(string root, string output)
    {
        using var source = new SingleMbTilesSource(Path.Combine(root, "tiles", "zurich.mbtiles"));
        var rows = new List<object>();
        foreach (var name in new[] { "basic", "bright" })
        foreach (var tile in new[] { (Z: 12, X: 2145, Y: 2662), (Z: 14, X: 8580, Y: 10646) })
        foreach (var size in new[] { 256, 512 })
        {
            var style = new Style(Path.Combine(root, "styles", name + "-style.json"))
            {
                FontDirectory = Path.Combine(root, "styles", "fonts")
            };
            style.SetSourceProvider(0, source);
            using var canvas = new CountingCanvas();
            using var bitmap = Renderer.Render(style, canvas, tile.X, tile.Y, tile.Z, size, size).GetAwaiter().GetResult();
            if (bitmap == null) throw new InvalidOperationException("Missing render.");
            rows.Add(new
            {
                Case = $"{name}-z{tile.Z}-{tile.X}-{tile.Y}-{size}",
                canvas.LineCalls, canvas.PolygonCalls, UniqueGeometry = canvas.Counts.Count,
                RepeatedCalls = canvas.Counts.Values.Sum() - canvas.Counts.Count,
                TotalPointsSubmitted = canvas.Counts.Sum(pair => (long)pair.Key.Count * pair.Value),
                UniquePoints = canvas.Counts.Sum(pair => (long)pair.Key.Count),
                Histogram = canvas.Counts.Values.GroupBy(count => count).OrderBy(group => group.Key)
                    .Select(group => new { Submissions = group.Key, Geometries = group.Count() })
            });
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Policy = "Real CPU render, count renderer line/polygon submissions by List identity. Diagnostic counts include no timings. Point-marker internals and text paths are excluded. Geometry content is not mutated by this census.",
            Cases = rows
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(output);
    }

    private sealed class CountingCanvas : SkiaCanvas, ICanvas
    {
        public Dictionary<List<Point>, int> Counts { get; } = new();
        public List<(bool Polygon, List<Point> Geometry, Brush Brush)> Commands { get; } = new();
        public int LineCalls { get; private set; }
        public int PolygonCalls { get; private set; }

        void ICanvas.DrawLineString(List<Point> geometry, Brush brush)
        {
            Counts[geometry] = Counts.GetValueOrDefault(geometry) + 1;
            LineCalls++;
            Commands.Add((false, geometry, brush));
            base.DrawLineString(geometry, brush);
        }

        void ICanvas.DrawPolygon(List<Point> geometry, Brush brush)
        {
            Counts[geometry] = Counts.GetValueOrDefault(geometry) + 1;
            PolygonCalls++;
            Commands.Add((true, geometry, brush));
            base.DrawPolygon(geometry, brush);
        }
    }
}
