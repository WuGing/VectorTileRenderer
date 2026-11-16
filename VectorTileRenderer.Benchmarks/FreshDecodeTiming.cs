using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Mapbox.Vector.Tile;
using ProtoBuf;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Benchmarks;

internal static class FreshDecodeTiming
{
    public static void Run(string root, string output, int iterations)
    {
        if (iterations < 10) throw new ArgumentOutOfRangeException(nameof(iterations));
        var dataset = Path.Combine(root, "tiles", "zurich.mbtiles");
        using var warm = new SingleMbTilesSource(dataset);
        using var raw = warm.GetRawTile(8580, 10646, 14);
        using var buffer = new MemoryStream();
        raw.CopyTo(buffer);
        var compressed = buffer.ToArray();
        var plain = ParserComparison.LoadPbf(root);
        using var modelInput = new MemoryStream(plain, false);
        var model = Serializer.Deserialize<Tile>(modelInput);
        var workloads = new (string Name, Action Run)[]
        {
            ("provider-open-metadata", () => { using var source = new SingleMbTilesSource(dataset); }),
            ("sqlite-raw-read", () => { using var stream = warm.GetRawTile(8580, 10646, 14); }),
            ("inflate-buffer", () =>
            {
                using var input = new MemoryStream(compressed, false);
                using var zip = new GZipStream(input, CompressionMode.Decompress);
                using var result = new MemoryStream();
                zip.CopyTo(result);
                if (result.Length != plain.Length) throw new InvalidOperationException("Inflation differs.");
            }),
            ("protobuf-model", () =>
            {
                using var input = new MemoryStream(plain, false);
                GC.KeepAlive(Serializer.Deserialize<Tile>(input));
            }),
            ("dependency-geometry", () =>
            {
                foreach (var layer in model.Layers)
                foreach (var feature in layer.Features)
                    GC.KeepAlive(GeometryParser.ParseGeometry(feature.Geometry, feature.Type));
            }),
            ("plain-renderer-decode", () =>
            {
                using var input = new MemoryStream(plain, false);
                GC.KeepAlive(new PbfTileSource(input).GetVectorTile(0, 0, 0).GetAwaiter().GetResult());
            }),
            ("gzip-renderer-decode", () =>
            {
                using var input = new MemoryStream(compressed, false);
                GC.KeepAlive(new PbfTileSource(input).GetVectorTile(0, 0, 0).GetAwaiter().GetResult());
            }),
            ("fresh-provider-decode", () =>
            {
                using var source = new SingleMbTilesSource(dataset);
                var tile = source.GetVectorTile(8580, 10646, 14).GetAwaiter().GetResult();
                if (tile?.Layers.Count != model.Layers.Count) throw new InvalidOperationException("Decode failed.");
                GC.KeepAlive(tile);
            })
        };
        var samples = new List<object>();
        for (var round = -10; round < iterations; round++)
        for (var offset = 0; offset < workloads.Length; offset++)
        {
            var workload = workloads[(round + 10 + offset) % workloads.Length];
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            workload.Run();
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (round >= 0) samples.Add(new { Round = round, Workload = workload.Name, ElapsedMs = elapsed, ManagedBytes = allocated });
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            LibrarySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(Style).Assembly.Location))),
            CompressedBytes = compressed.Length, PlainBytes = plain.Length, Iterations = iterations, Warmups = 10,
            Policy = "Rotating isolated attribution workloads, warmed OS file caches, one thread, no profiler/forced GC. Stages overlap and must not be summed. Inflate pilot materializes a buffer while production streams gzip. Managed counters exclude native memory.",
            Samples = samples
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(output);
    }
}
