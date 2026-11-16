using System.Diagnostics.Tracing;
using System.Linq;
using SkiaSharp;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Benchmarks;

internal static class RequestCpuWorkload
{
    [EventSource(Name = "VectorTileRenderer-Request")]
    private sealed class Markers : EventSource
    {
        public static readonly Markers Log = new();
        [Event(1, Opcode = EventOpcode.Info)] public void Window(string value) => WriteEvent(1, value);
    }

    public static void Run(string root, string outputParent, int count)
    {
        if (count < 10) throw new ArgumentOutOfRangeException(nameof(count));
        root = Path.GetFullPath(root);
        var cache = Path.Combine(Path.GetFullPath(outputParent), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cache);
        var dataset = Path.Combine(root, "tiles", "zurich.mbtiles");
        using var warm = new SingleMbTilesSource(dataset);
        var style = new Style(Path.Combine(root, "styles", "bright-style.json"))
        {
            FontDirectory = Path.Combine(root, "styles", "fonts")
        };
        style.SetSourceProvider(0, warm);
        var previousRenderSink = Renderer.ProfileSink;
        var previousCacheSink = Renderer.CacheProfileSink;
        try
        {
            Renderer.RenderProfile referenceProfile = null;
            Renderer.ProfileSink = profile => referenceProfile = profile;
            Renderer.CacheProfileSink = null;
            using var referenceCanvas = new SkiaCanvas();
            using var reference = Renderer.Render(style, referenceCanvas, 8580, 10646, 14, 512, 512).GetAwaiter().GetResult();
            if (reference == null || referenceProfile?.ActualBackend != "CPU" || referenceProfile.GeometryDrawCallCount == 0)
                throw new InvalidOperationException("A nonempty CPU reference is required.");
            var expectedPixels = reference.Pixels;
            Renderer.ProfileSink = null;
            byte[] Request(string phase)
            {
                using var fresh = phase == "fresh-miss" ? new SingleMbTilesSource(dataset) : null;
                using var canvas = new SkiaCanvas();
                style.SetSourceProvider(0, fresh ?? warm);
                var revision = phase == "hit" ? "hit" : Guid.NewGuid().ToString("N");
                return Renderer.RenderCachedPng(cache, style, canvas, 8580, 10646, 14, revision, 512, 512)
                    .GetAwaiter().GetResult() ?? throw new InvalidOperationException("No PNG output.");
            }
            Request("hit");
            var phases = new[] { "fresh-miss", "warm-miss", "hit" };
            foreach (var phase in phases)
                for (var i = 0; i < 5; i++) Request(phase);
            var outputs = new List<byte[]>();
            foreach (var phase in phases)
            {
                byte[] last = null;
                var thread = Environment.CurrentManagedThreadId;
                Markers.Log.Window("start|" + phase);
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < count; i++) last = Request(phase);
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                if (thread != Environment.CurrentManagedThreadId) throw new InvalidOperationException("Request crossed threads.");
                Markers.Log.Window(FormattableString.Invariant($"end|{phase}|{allocated}|{count}"));
                outputs.Add(last);
                Console.WriteLine($"{phase}: {count} requests, {allocated / (double)count / 1048576:F2} MiB managed/request");
            }
            // Keep all validation and generated-cache pruning outside marked windows.
            foreach (var bytes in outputs)
            {
                using var decoded = SKBitmap.Decode(bytes);
                if (decoded == null || !decoded.Pixels.SequenceEqual(expectedPixels))
                    throw new InvalidOperationException("Request output differs from reference.");
            }
            var files = Directory.GetFiles(cache, "*.png");
            if (files.Length != 1 + 2 * (count + 5) || Directory.GetFiles(cache, "*.tmp").Length != 0)
                throw new InvalidOperationException("Unexpected cache publication count.");
            foreach (var file in files)
            {
                using var decoded = SKBitmap.Decode(file);
                if (decoded == null || !decoded.Pixels.SequenceEqual(expectedPixels))
                    throw new InvalidOperationException("Published PNG differs from reference.");
            }
            TileCache.Prune(cache, 0);
            Console.WriteLine("All published PNGs match the CPU reference.");
        }
        finally
        {
            Renderer.ProfileSink = previousRenderSink;
            Renderer.CacheProfileSink = previousCacheSink;
        }
    }
}
