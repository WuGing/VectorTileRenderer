using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading.Tasks;
using SkiaSharp;

namespace VectorTileRenderer.Benchmarks;

internal static class PairedRequestComparison
{
    public static void Run(string root, string baselineAssembly, string output, int iterations)
    {
        if (iterations < 10) throw new ArgumentOutOfRangeException(nameof(iterations));
        root = Path.GetFullPath(root);
        var cache = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)), "paired-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cache);
        var context = new AssemblyLoadContext("paired-request-baseline", isCollectible: true);
        try
        {
            var baseline = context.LoadFromAssemblyPath(Path.GetFullPath(baselineAssembly));
            using var before = new Implementation(baseline, root, Path.Combine(cache, "baseline"));
            using var after = new Implementation(typeof(Style).Assembly, root, Path.Combine(cache, "candidate"));
            using var reference = SKBitmap.Decode(before.Request("warm-miss"));
            var pixels = reference.Pixels;
            var modes = new[] { "fresh-miss", "warm-miss", "hit" };
            var samples = new List<object>();
            for (var round = -5; round < iterations; round++)
            for (var modeIndex = 0; modeIndex < modes.Length; modeIndex++)
            for (var offset = 0; offset < 2; offset++)
            {
                var mode = modes[(round + 5 + modeIndex) % modes.Length];
                var candidate = (round + 5 + offset) % 2 == 0;
                var implementation = candidate ? after : before;
                var thread = Environment.CurrentManagedThreadId;
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var started = Stopwatch.GetTimestamp();
                var bytes = implementation.Request(mode);
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                if (thread != Environment.CurrentManagedThreadId) throw new InvalidOperationException("Request crossed threads.");
                using var decoded = SKBitmap.Decode(bytes);
                if (decoded == null || !decoded.Pixels.SequenceEqual(pixels))
                    throw new InvalidOperationException("Baseline/candidate pixels differ.");
                if (round >= 0) samples.Add(new { Round = round, Mode = mode,
                    Implementation = candidate ? "candidate" : "baseline", ElapsedMs = elapsed, ManagedBytes = allocated });
            }
            foreach (var directory in new[] { before.Cache, after.Cache })
            {
                if (Directory.GetFiles(directory, "*.tmp").Length != 0)
                    throw new InvalidOperationException("Temporary cache publications remain.");
                foreach (var file in Directory.GetFiles(directory, "*.png"))
                {
                    using var decoded = SKBitmap.Decode(file);
                    if (decoded == null || !decoded.Pixels.SequenceEqual(pixels))
                        throw new InvalidOperationException("Published pixels differ.");
                }
                TileCache.Prune(directory, 0);
            }
            File.WriteAllText(output, JsonSerializer.Serialize(new
            {
                Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                BaselineSha256 = Hash(baseline.Location), CandidateSha256 = Hash(typeof(Style).Assembly.Location),
                Iterations = iterations, Warmups = 5, PixelEquality = true, Tile = "zurich-z14-8580-10646", Size = 512,
                Policy = "Two assemblies in one process, rotating fresh/warm/hit modes and alternating baseline/candidate order each round. Observers off, warmed OS file caches, reused style, new canvas per request. Includes reflected invocation, setup/disposal, encoding and publication. Exact pixels checked outside timers after every request and for all publications. Managed counters exclude native memory. No forced GC.",
                Samples = samples
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(output);
        }
        finally
        {
            context.Unload();
        }
    }

    private static string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));

    private sealed class Implementation : IDisposable
    {
        private readonly Type sourceType;
        private readonly Type canvasType;
        private readonly MethodInfo setProvider;
        private readonly MethodInfo render;
        private readonly object style;
        private readonly IDisposable warmSource;
        private readonly string dataset;
        public string Cache { get; }

        public Implementation(Assembly assembly, string root, string cache)
        {
            Cache = cache;
            Directory.CreateDirectory(cache);
            sourceType = assembly.GetType("WuGing.VectorTileRenderer.Sources.SingleMbTilesSource");
            canvasType = assembly.GetType(typeof(SkiaCanvas).FullName);
            var styleType = assembly.GetType(typeof(Style).FullName);
            dataset = Path.Combine(root, "tiles", "zurich.mbtiles");
            warmSource = (IDisposable)Activator.CreateInstance(sourceType, dataset);
            style = Activator.CreateInstance(styleType, Path.Combine(root, "styles", "bright-style.json"));
            styleType.GetProperty(nameof(Style.FontDirectory)).SetValue(style, Path.Combine(root, "styles", "fonts"));
            setProvider = styleType.GetMethods().Single(method => method.Name == nameof(Style.SetSourceProvider)
                && method.GetParameters()[0].ParameterType == typeof(int));
            render = assembly.GetType(typeof(Renderer).FullName).GetMethods().Single(method => method.Name == nameof(Renderer.RenderCachedPng)
                && method.GetParameters().Length == 11);
            Request("hit");
        }

        public byte[] Request(string mode)
        {
            using var source = mode == "fresh-miss" ? (IDisposable)Activator.CreateInstance(sourceType, dataset) : null;
            using var canvas = (IDisposable)Activator.CreateInstance(canvasType);
            setProvider.Invoke(style, [0, source ?? warmSource]);
            var revision = mode == "hit" ? "hit" : Guid.NewGuid().ToString("N");
            var task = (Task<byte[]>)render.Invoke(null, [Cache, style, canvas, 8580, 10646, 14d, revision, 512d, 512d, 1d, null]);
            return task.GetAwaiter().GetResult() ?? throw new InvalidOperationException("No PNG output.");
        }

        public void Dispose() => warmSource.Dispose();
    }
}
