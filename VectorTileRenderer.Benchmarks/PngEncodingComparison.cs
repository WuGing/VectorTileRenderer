using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using SkiaSharp;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Benchmarks;

internal static class PngEncodingComparison
{
    private sealed record Mode(string Name, SKPngEncoderFilterFlags Filters, int Level, bool Legacy = false);

    public static void Run(string root, string output, int iterations)
    {
        if (iterations < 10) throw new ArgumentOutOfRangeException(nameof(iterations));
        var modes = new[]
        {
            new Mode("legacy", SKPngEncoderFilterFlags.AllFilters, 6, true),
            new Mode("all-1", SKPngEncoderFilterFlags.AllFilters, 1),
            new Mode("all-3", SKPngEncoderFilterFlags.AllFilters, 3),
            new Mode("all-6", SKPngEncoderFilterFlags.AllFilters, 6),
            new Mode("sub-up-1", SKPngEncoderFilterFlags.Sub | SKPngEncoderFilterFlags.Up, 1),
            new Mode("sub-up-3", SKPngEncoderFilterFlags.Sub | SKPngEncoderFilterFlags.Up, 3),
            new Mode("up-1", SKPngEncoderFilterFlags.Up, 1),
            new Mode("none-1", SKPngEncoderFilterFlags.None, 1),
            new Mode("all-0", SKPngEncoderFilterFlags.AllFilters, 0)
        };
        using var source = new SingleMbTilesSource(Path.Combine(root, "tiles", "zurich.mbtiles"));
        var rows = new List<object>();
        void Measure(string caseName, SKBitmap bitmap)
        {
            using var referenceData = Encode(bitmap, modes[0]);
            using var reference = SKBitmap.Decode(referenceData);
            var expectedPixels = reference.Pixels;
            if (expectedPixels.Distinct().Take(2).Count() < 2) throw new InvalidOperationException("Uniform reference.");
            var samples = modes.ToDictionary(mode => mode.Name, _ => new List<object>());
            for (var round = -5; round < iterations; round++)
            for (var offset = 0; offset < modes.Length; offset++)
            {
                var mode = modes[(round + 5 + offset) % modes.Length];
                var thread = Environment.CurrentManagedThreadId;
                var before = GC.GetAllocatedBytesForCurrentThread();
                var started = Stopwatch.GetTimestamp();
                using var data = Encode(bitmap, mode);
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                if (Environment.CurrentManagedThreadId != thread) throw new InvalidOperationException("Encode crossed threads.");
                using var decoded = SKBitmap.Decode(data);
                if (decoded == null || !decoded.Pixels.SequenceEqual(expectedPixels))
                    throw new InvalidOperationException($"Pixel mismatch for {caseName}/{mode.Name}.");
                if (round >= 0) samples[mode.Name].Add(new { ElapsedMs = elapsed, ManagedBytes = allocated, OutputBytes = data.Size });
            }
            foreach (var mode in modes)
                rows.Add(new { Case = caseName, mode.Name, Filters = mode.Filters.ToString(), mode.Level, PixelEquality = true, Samples = samples[mode.Name] });
            Console.WriteLine(caseName + " encoding and pixel checks complete.");
        }
        foreach (var tile in new[] { (Z: 12, X: 2145, Y: 2662), (Z: 14, X: 8580, Y: 10646) })
        foreach (var styleName in new[] { "basic", "bright" })
        foreach (var size in new[] { 256, 512 })
        {
            var style = new Style(Path.Combine(root, "styles", styleName + "-style.json"))
            {
                FontDirectory = Path.Combine(root, "styles", "fonts")
            };
            style.SetSourceProvider(0, source);
            using var canvas = new SkiaCanvas();
            using var bitmap = Renderer.Render(style, canvas, tile.X, tile.Y, tile.Z, size, size).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Required tile is missing.");
            Measure($"{styleName}-z{tile.Z}-{size}", bitmap);
        }
        foreach (var kind in new[] { "gradient", "noise", "alpha-checker" })
        {
            using var bitmap = new SKBitmap(512, 512);
            var random = new Random(2849);
            for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = kind switch
                {
                    "gradient" => new SKColor((byte)(x / 2), (byte)(y / 2), (byte)((x + y) / 4), (byte)(64 + x / 4)),
                    "noise" => new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)),
                    _ => new SKColor(220, 40, 80, (byte)(((x / 16 + y / 16) % 3) * 127))
                };
                bitmap.SetPixel(x, y, color);
            }
            Measure("synthetic-" + kind, bitmap);
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            DefaultFilters = SKPngEncoderOptions.Default.FilterFlags.ToString(),
            DefaultLevel = SKPngEncoderOptions.Default.ZLibLevel, Iterations = iterations, Warmups = 5,
            Policy = "Rotating encoder modes on fixed CPU bitmaps, encode/result creation timed, validation and data disposal outside timers. Managed counters exclude native allocations. No profiler.",
            Results = rows
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(output);
    }

    private static SKData Encode(SKBitmap bitmap, Mode mode)
    {
        if (mode.Legacy)
        {
            using var image = SKImage.FromBitmap(bitmap);
            return image.Encode(SKEncodedImageFormat.Png, 100);
        }
        using var pixels = bitmap.PeekPixels();
        return pixels.Encode(new SKPngEncoderOptions(mode.Filters, mode.Level));
    }
}
