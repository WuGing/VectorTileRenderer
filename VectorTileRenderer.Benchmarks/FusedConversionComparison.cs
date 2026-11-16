using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Benchmarks;

internal static class FusedConversionComparison
{
    public static void Run(string root, string output, int iterations)
    {
        if (iterations < 10) throw new ArgumentOutOfRangeException(nameof(iterations));
        var bytes = ParserComparison.LoadPbf(root);
        var parser = typeof(PbfTileSource).Assembly.GetType("WuGing.VectorTileRenderer.Sources.PbfParser");
        var staged = parser.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)
            .CreateDelegate<Func<Stream, List<Mapbox.Vector.Tile.VectorTileLayer>>>();
        var convert = typeof(PbfTileSource).GetMethod("BaseTileToVector", BindingFlags.NonPublic | BindingFlags.Static)
            .CreateDelegate<Func<List<Mapbox.Vector.Tile.VectorTileLayer>, VectorTile>>();
        var fused = parser.GetMethod("ParseRendererTile", BindingFlags.Public | BindingFlags.Static)
            .CreateDelegate<Func<Stream, VectorTile>>();
        Func<Stream, VectorTile> baseline = input => convert(staged(input));
        var samples = new List<object>();
        for (var round = -10; round < iterations; round++)
        {
            for (var offset = 0; offset < 2; offset++)
            {
                var useCandidate = (round + 10 + offset) % 2 == 0;
                var parse = useCandidate ? fused : baseline;
                using var input = new MemoryStream(bytes, false);
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var started = Stopwatch.GetTimestamp();
                var result = parse(input);
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                if (result.Layers.Count != 11 || result.Layers.Sum(layer => layer.Features.Count) != 16241)
                    throw new InvalidOperationException("Unexpected decoded output.");
                GC.KeepAlive(result);
                if (round >= 0) samples.Add(new { Round = round, Mode = useCandidate ? "fused" : "staged", ElapsedMs = elapsed, ManagedBytes = allocated });
            }
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Tile = "zurich-z14-8580-10646", Iterations = iterations, Warmups = 10,
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            Policy = "Pre-decompressed PBF to final renderer model, alternating order, one thread, no profiler, includes protobuf parsing and model conversion, excludes rendering and SQL.",
            Samples = samples
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(output);
    }
}
