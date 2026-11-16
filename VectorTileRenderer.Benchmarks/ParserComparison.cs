using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Runtime.Loader;
using Mapbox.Vector.Tile;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Benchmarks;

internal static class ParserComparison
{
    public static void Run(string root, string output, int iterations, string baselineAssembly = null, bool protobufExperiment = false)
    {
        if (iterations < 10) throw new ArgumentOutOfRangeException(nameof(iterations));
        var bytes = LoadPbf(root);
        var candidate = typeof(PbfTileSource).Assembly.GetType("WuGing.VectorTileRenderer.Sources.PbfParser")
            .GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)
            .CreateDelegate<Func<Stream, List<Mapbox.Vector.Tile.VectorTileLayer>>>();
        Func<Stream, List<Mapbox.Vector.Tile.VectorTileLayer>> baseline = VectorTileParser.Parse;
        if (protobufExperiment)
        {
            baseline = candidate;
            candidate = ProtobufReadExperiment.Parse;
        }
        var baselineContext = baselineAssembly == null ? null : new AssemblyLoadContext("Parser comparison baseline");
        if (baselineContext != null)
        {
            var assembly = baselineContext.LoadFromAssemblyPath(Path.GetFullPath(baselineAssembly));
            baseline = assembly.GetType("WuGing.VectorTileRenderer.Sources.PbfParser")
                .GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)
                .CreateDelegate<Func<Stream, List<Mapbox.Vector.Tile.VectorTileLayer>>>();
        }
        var samples = new List<object>();
        for (var round = -10; round < iterations; round++)
        {
            for (var offset = 0; offset < 2; offset++)
            {
                var useCandidate = (round + 10 + offset) % 2 == 0;
                var parse = useCandidate ? candidate : baseline;
                using var input = new MemoryStream(bytes, false);
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var started = Stopwatch.GetTimestamp();
                var result = parse(input);
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                if (result.Count != 11 || result.Sum(layer => layer.VectorTileFeatures.Count) != 16241)
                    throw new InvalidOperationException("Unexpected parser output.");
                GC.KeepAlive(result);
                if (round >= 0) samples.Add(new { Round = round, Mode = useCandidate ? "candidate" : "baseline", ElapsedMs = elapsed, ManagedBytes = allocated });
            }
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Tile = "zurich-z14-8580-10646", Iterations = iterations, Warmups = 10,
            BaselineAssembly = baselineAssembly == null
                ? (protobufExperiment ? "current production parser" : "mapbox-vector-tile dependency")
                : Path.GetFullPath(baselineAssembly),
            Candidate = protobufExperiment ? "isolated direct-append protobuf reader" : "current production parser",
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            Policy = "Pre-decompressed PBF, alternating parser order, one thread, no profiler, includes parser result construction, excludes adapter/rendering.",
            Samples = samples
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(output);
    }

    internal static byte[] LoadPbf(string root)
    {
        using var source = new SingleMbTilesSource(Path.Combine(root, "tiles", "zurich.mbtiles"));
        using var raw = source.GetRawTile(8580, 10646, 14);
        var gzip = raw.ReadByte() == 0x1f && raw.ReadByte() == 0x8b;
        raw.Position = 0;
        using var buffer = new MemoryStream();
        if (gzip)
        {
            using var decompressed = new GZipStream(raw, CompressionMode.Decompress, true);
            decompressed.CopyTo(buffer);
        }
        else raw.CopyTo(buffer);
        return buffer.ToArray();
    }
}
