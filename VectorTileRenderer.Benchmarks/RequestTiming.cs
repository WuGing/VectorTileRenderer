using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Benchmarks;

// Sequential exploratory end-to-end measurements, separate from BenchmarkDotNet microbenchmarks.
internal static class RequestTiming
{
    private sealed record Tile(int Zoom, int X, int TmsY, int CompressedBytes);
    private sealed record Mode(string Name, bool Cached, bool Fresh, bool Hit, bool ClientEncode = false, bool DirectPng = false);
    private sealed record Sample(double TotalMs, double SetupMs, double RequestMs, double LookupMs,
        double RenderMs, double FetchMs, double BuildMs, double StyleMs, double GeometryMs,
        double TextMs, double FinishMs, double EncodePublishMs, double ClientEncodeMs,
        long ManagedBytes, int Gen0, int Gen1, int Gen2);

    public static int Run(string[] args)
    {
        var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        var output = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(root, "artifacts", "request-timing"));
        var iterations = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 50;
        var caseFilter = args.Length > 3 ? args[3] : null;
        var pngComparison = args.Length > 4 && args[4] == "png-comparison";
        if (iterations < 10) throw new ArgumentOutOfRangeException(nameof(args), "Use at least ten measured rounds.");
        // A unique subdirectory prevents overwriting earlier runs or touching caller cache data.
        output = Path.Combine(output, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(output);
        var dataset = Path.Combine(root, "tiles", "zurich.mbtiles");
        var rows = new List<object>();
        var modes = new[]
        {
            new Mode("render-warm", false, false, false),
            new Mode("render-fresh", false, true, false),
            new Mode("miss-warm", true, false, false),
            new Mode("miss-fresh", true, true, false),
            new Mode("hit", true, false, true),
            new Mode("miss-warm-client-png", true, false, false, true),
            new Mode("hit-client-png", true, false, true, true),
        };
        if (pngComparison)
        {
            modes =
            [
                new("miss-warm-client-png", true, false, false, true),
                new("miss-fresh-client-png", true, true, false, true),
                new("hit-client-png", true, false, true, true),
                new("miss-warm-direct-png", true, false, false, DirectPng: true),
                new("miss-fresh-direct-png", true, true, false, DirectPng: true),
                new("hit-direct-png", true, false, true, DirectPng: true),
            ];
        }
        var previousRenderSink = Renderer.ProfileSink;
        var previousCacheSink = Renderer.CacheProfileSink;
        Renderer.RenderProfile renderProfile = null;
        CacheRequestProfile cacheProfile = null;
        Renderer.ProfileSink = profile => renderProfile = profile;
        Renderer.CacheProfileSink = profile => cacheProfile = profile;
        var tiles = SelectTiles(dataset);
        var environment = new
        {
            TimestampUtc = DateTime.UtcNow, Runtime = RuntimeInformation.FrameworkDescription,
            OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            LogicalProcessors = Environment.ProcessorCount, Skia = typeof(SKBitmap).Assembly.GetName().Version?.ToString(),
            Processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"), HarnessVersion = 2,
            ModeSet = pngComparison ? "PNG comparison; bitmap path includes client bitmap disposal" : "Original baseline",
            CaseFilter = caseFilter,
            Dataset = Path.GetFileName(dataset), DatasetBytes = new FileInfo(dataset).Length,
            DatasetSha256 = HashFile(dataset), Iterations = iterations, Warmups = 5, Concurrency = 1,
            CachePolicy = "OS/SQLite file caches uncontrolled and warmed; fresh means new provider including metadata/cache/disposal; new canvas per request; style reused; miss uses unique revision",
            TimingPolicy = "External total includes canvas/provider setup, request, optional client PNG encoding and canvas/provider disposal; excludes output pixel validation, returned bitmap disposal and JSON reporting. Both profiling observers enabled.",
            AllocationPolicy = "Current-thread managed bytes, checked same thread; excludes native memory and validation. No forced GC between requests.",
            Ordering = $"Rotate {modes.Length} modes each round; cases/styles/sizes sequential; no host-queue/UI/GPU timings", Tiles = tiles,
            ValidationPolicy = "Exact pixels checked after every request outside timers; validation allocation/GC can affect subsequent samples. All published PNGs decoded and compared after each case.",
            FontHashes = Directory.GetFiles(Path.Combine(root, "styles", "fonts"))
                .OrderBy(path => path, StringComparer.Ordinal).ToDictionary(Path.GetFileName, HashFile),
        };
        try
        {
            foreach (var tile in tiles)
            foreach (var styleName in new[] { "basic", "bright" })
            foreach (var size in new[] { 256, 512 })
            {
                var caseName = $"{styleName}-z{tile.Zoom}-{tile.X}-{tile.TmsY}-{size}";
                if (caseFilter != null && caseFilter != "*" && caseName != caseFilter) continue;
                using var warm = new SingleMbTilesSource(dataset);
                var stylePath = Path.Combine(root, "styles", styleName + "-style.json");
                var style = new Style(stylePath) { FontDirectory = Path.Combine(root, "styles", "fonts") };
                style.SetSourceProvider(0, warm);
                var decoded = warm.GetVectorTile(tile.X, tile.TmsY, tile.Zoom).GetAwaiter().GetResult()
                    ?? throw new InvalidOperationException("Selected native tile is missing.");
                var featureCount = decoded.Layers.Sum(layer => layer.Features.Count);
                using var referenceCanvas = new SkiaCanvas();
                using var reference = Renderer.Render(style, referenceCanvas, tile.X, tile.TmsY, tile.Zoom, size, size).GetAwaiter().GetResult()
                    ?? throw new InvalidOperationException("No reference image.");
                if (renderProfile?.ActualBackend != "CPU" || renderProfile.GeometryDrawCallCount == 0)
                    throw new InvalidOperationException("Workload must draw CPU geometry before timing.");
                var expectedPixels = reference.Pixels;
                if (expectedPixels.Distinct().Take(2).Count() < 2) throw new InvalidOperationException("Uniform reference output.");
                using (var data = reference.Encode(SKEncodedImageFormat.Png, 100))
                    File.WriteAllBytes(Path.Combine(output, caseName + ".png"), data.ToArray());
                var cache = Path.Combine(output, "cache", caseName);
                var samples = modes.ToDictionary(mode => mode.Name, _ => new List<Sample>());
                // Seed the hit outside measurements, with its own stable revision.
                using (var seedCanvas = new SkiaCanvas())
                using (var seed = Renderer.RenderCached(cache, style, seedCanvas, tile.X, tile.TmsY, tile.Zoom, "hit", size, size).GetAwaiter().GetResult())
                {
                    if (seed == null) throw new InvalidOperationException("Cache seed missing.");
                }
                for (var round = -5; round < iterations; round++)
                for (var offset = 0; offset < modes.Length; offset++)
                {
                    var mode = modes[((round + 5) + offset) % modes.Length];
                    renderProfile = null;
                    cacheProfile = null;
                    SKBitmap bitmap = null;
                    byte[] png = null;
                    SingleMbTilesSource fresh = null;
                    var thread = Environment.CurrentManagedThreadId;
                    var gcBefore = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
                    var allocated = GC.GetAllocatedBytesForCurrentThread();
                    var started = Stopwatch.GetTimestamp();
                    double setupMs, requestMs, clientEncodeMs = 0;
                    try
                    {
                        using var canvas = new SkiaCanvas();
                        fresh = mode.Fresh ? new SingleMbTilesSource(dataset) : null;
                        style.SetSourceProvider(0, fresh ?? warm);
                        var requestStarted = Stopwatch.GetTimestamp();
                        setupMs = Ms(started, requestStarted);
                        if (mode.DirectPng)
                            png = Renderer.RenderCachedPng(cache, style, canvas, tile.X, tile.TmsY, tile.Zoom,
                                mode.Hit ? "hit" : $"{mode.Name}:{round}", size, size).GetAwaiter().GetResult();
                        else bitmap = mode.Cached
                            ? Renderer.RenderCached(cache, style, canvas, tile.X, tile.TmsY, tile.Zoom,
                                mode.Hit ? "hit" : $"{mode.Name}:{round}", size, size).GetAwaiter().GetResult()
                            : Renderer.Render(style, canvas, tile.X, tile.TmsY, tile.Zoom, size, size).GetAwaiter().GetResult();
                        var requestFinished = Stopwatch.GetTimestamp();
                        requestMs = Ms(requestStarted, requestFinished);
                        if (mode.ClientEncode)
                        {
                            using var image = SKImage.FromBitmap(bitmap);
                            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                            png = data.ToArray(); // Match Mapsui's consumer-owned encoded output.
                            GC.KeepAlive(png);
                            clientEncodeMs = Ms(requestFinished, Stopwatch.GetTimestamp());
                            if (pngComparison)
                            {
                                bitmap.Dispose();
                                bitmap = null;
                            }
                        }
                    }
                    finally
                    {
                        fresh?.Dispose();
                    }
                    var finished = Stopwatch.GetTimestamp();
                    var managedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    var totalMs = Ms(started, finished);
                    var gcAfter = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
                    using (bitmap)
                    using (var decodedPng = png == null ? null : SKBitmap.Decode(png))
                    {
                        if (thread != Environment.CurrentManagedThreadId) throw new InvalidOperationException("Allocation measurement crossed threads.");
                        var result = decodedPng ?? bitmap;
                        if (result == null || !result.Pixels.SequenceEqual(expectedPixels))
                            throw new InvalidOperationException($"Pixel mismatch: {caseName}/{mode.Name}/{round}");
                        if (mode.Cached && (cacheProfile == null || cacheProfile.CacheHit != mode.Hit || !cacheProfile.HasOutput))
                            throw new InvalidOperationException("Cache outcome differs from declared workload.");
                        if (mode.Hit ? renderProfile != null : renderProfile?.ActualBackend != "CPU")
                            throw new InvalidOperationException("Unexpected render backend or render on cache hit.");
                    }
                    if (round >= 0)
                        samples[mode.Name].Add(new Sample(totalMs, setupMs, requestMs, cacheProfile?.LookupMs ?? 0,
                            cacheProfile?.RenderMs ?? renderProfile.TotalMs, renderProfile?.TileFetchDecodeMs ?? 0,
                            renderProfile?.BuildVisualLayersMs ?? 0, renderProfile?.BuildStyleEvalMs ?? 0,
                            renderProfile?.DrawGeometryMs ?? 0, renderProfile?.DrawTextOnlyMs ?? 0,
                            renderProfile?.FinishDrawingMs ?? 0, cacheProfile?.EncodeAndPublishMs ?? 0,
                            clientEncodeMs, managedBytes, gcAfter[0] - gcBefore[0], gcAfter[1] - gcBefore[1], gcAfter[2] - gcBefore[2]));
                }
                var published = Directory.GetFiles(cache, "*.png");
                var expectedFileCount = 1 + modes.Count(mode => mode.Cached && !mode.Hit) * (iterations + 5);
                if (published.Length != expectedFileCount || Directory.GetFiles(cache, "*.tmp").Length != 0)
                    throw new InvalidOperationException("Missing cache publication or leftover temporary file.");
                foreach (var path in published)
                {
                    using var image = SKBitmap.Decode(path);
                    if (image == null || !image.Pixels.SequenceEqual(expectedPixels))
                        throw new InvalidOperationException("Published PNG pixels differ from reference.");
                }
                foreach (var mode in modes)
                {
                    var values = samples[mode.Name];
                    var summary = typeof(Sample).GetProperties().ToDictionary(property => property.Name, property => new
                    {
                        Median = Percentile(values.Select(sample => Convert.ToDouble(property.GetValue(sample), CultureInfo.InvariantCulture)), 0.5),
                        P95 = Percentile(values.Select(sample => Convert.ToDouble(property.GetValue(sample), CultureInfo.InvariantCulture)), 0.95),
                    });
                    rows.Add(new { Case = caseName, StyleSha256 = HashFile(stylePath), tile, Size = size,
                        FeatureCount = featureCount, Mode = mode.Name, Backend = "CPU", PixelEquality = true,
                        Summary = summary, Samples = values });
                    Console.WriteLine($"{caseName} {mode.Name}: median {summary[nameof(Sample.TotalMs)].Median:F2} ms, p95 {summary[nameof(Sample.TotalMs)].P95:F2} ms");
                }
                // Remove only generated PNGs via the production maintenance API, outside timings.
                TileCache.Prune(cache, 0);
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { Environment = environment, Results = rows }, new JsonSerializerOptions { WriteIndented = true }));
            }
            if (rows.Count == 0) throw new ArgumentException("No workload matched the case filter.");
            Console.WriteLine("Results: " + Path.Combine(output, "results.json"));
            return 0;
        }
        finally
        {
            Renderer.ProfileSink = previousRenderSink;
            Renderer.CacheProfileSink = previousCacheSink;
        }
    }

    private static Tile[] SelectTiles(string path)
    {
        using var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder
            { DataSource = path, ReadOnly = true, FailIfMissing = true }.ConnectionString);
        connection.Open();
        var tiles = new List<Tile>();
        foreach (var zoom in new[] { 12, 14 })
        {
            using var command = new SQLiteCommand("SELECT tile_column, tile_row, length(tile_data) FROM tiles WHERE zoom_level = @zoom ORDER BY length(tile_data) DESC, tile_column, tile_row LIMIT 1", connection);
            command.Parameters.AddWithValue("@zoom", zoom);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) throw new InvalidOperationException($"No native Zurich tile at zoom {zoom}.");
            tiles.Add(new Tile(zoom, reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)));
        }
        return tiles.ToArray();
    }

    private static string HashFile(string path)
    {
        using var hash = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(hash.ComputeHash(stream));
    }

    private static double Ms(long start, long end) => (end - start) * 1000.0 / Stopwatch.Frequency;
    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.Order().ToArray();
        return sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];
    }
}
