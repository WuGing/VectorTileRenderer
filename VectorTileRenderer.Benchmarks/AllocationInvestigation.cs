using System.Diagnostics.Tracing;
using System.Linq;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing.Etlx;
using SkiaSharp;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Benchmarks;

internal static class AllocationInvestigation
{
    [EventSource(Name = "VectorTileRenderer-Allocation")]
    private sealed class Markers : EventSource
    {
        public static readonly Markers Log = new();
        [Event(1, Opcode = EventOpcode.Info)] public void Window(string value) => WriteEvent(1, value);
    }

    public static void Run(string[] args, bool parserCpu = false)
    {
        var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        var count = args.Length > 1 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 100;
        if (count < 10) throw new ArgumentOutOfRangeException(nameof(args));
        var dataset = Path.Combine(root, "tiles", "zurich.mbtiles");
        using var warm = new SingleMbTilesSource(dataset);
        var tile = warm.GetVectorTile(8580, 10646, 14).GetAwaiter().GetResult()
            ?? throw new InvalidOperationException("Required native tile is missing.");
        var style = new Style(Path.Combine(root, "styles", "bright-style.json"))
        {
            FontDirectory = Path.Combine(root, "styles", "fonts")
        };
        style.SetSourceProvider(0, warm);
        using var raw = warm.GetRawTile(8580, 10646, 14);
        var gzip = raw.ReadByte() == 0x1f && raw.ReadByte() == 0x8b;
        raw.Position = 0;
        using var buffer = new MemoryStream();
        if (gzip)
        {
            using var decompressed = new GZipStream(raw, CompressionMode.Decompress, true);
            decompressed.CopyTo(buffer);
        }
        else raw.CopyTo(buffer);
        var pbf = buffer.ToArray();
        using var parsedStream = new MemoryStream(pbf, false);
        var parsed = Mapbox.Vector.Tile.VectorTileParser.Parse(parsedStream);
        var convert = typeof(PbfTileSource).GetMethod("BaseTileToVector", BindingFlags.NonPublic | BindingFlags.Static)
            .CreateDelegate<Func<List<Mapbox.Vector.Tile.VectorTileLayer>, VectorTile>>();
        foreach (var property in typeof(Mapbox.Vector.Tile.VectorTileFeature).GetProperties())
            Console.WriteLine($"Parser feature {property.Name}: {property.PropertyType}");
        // No profiling callbacks, PNG encoding or pixel-array validation inside allocation windows.
        Renderer.ProfileSink = null;
        Renderer.CacheProfileSink = null;
        Action decode = () =>
        {
            using var source = new SingleMbTilesSource(dataset);
            var decoded = source.GetVectorTile(8580, 10646, 14).GetAwaiter().GetResult();
            if (decoded?.Layers.Count != tile.Layers.Count) throw new InvalidOperationException("Decode failed.");
        };
        Action render = () => Draw(style, new SkiaCanvas());
        Action build = () => Draw(style, new NoDrawCanvas());
        Action parser = () =>
        {
            using var stream = new MemoryStream(pbf, false);
            var result = Mapbox.Vector.Tile.VectorTileParser.Parse(stream);
            if (result.Count != parsed.Count) throw new InvalidOperationException("Parser layer count changed.");
            GC.KeepAlive(result);
        };
        Action conversion = () =>
        {
            var result = convert(parsed);
            if (result.Layers.Count != tile.Layers.Count) throw new InvalidOperationException("Conversion layer count changed.");
            GC.KeepAlive(result);
        };
        var phases = parserCpu
            ? new[] { (Name: "parser-only", Action: parser), (Name: "conversion-only", Action: conversion), (Name: "fresh-decode", Action: decode) }
            : new[] { (Name: "warm-render", Action: render), (Name: "fresh-decode", Action: decode),
                (Name: "warm-build-no-draw", Action: build), (Name: "parser-only", Action: parser), (Name: "conversion-only", Action: conversion) };
        foreach (var action in phases.Select(phase => phase.Action))
            for (var i = 0; i < 5; i++) action();
        foreach (var phase in phases)
        {
            Markers.Log.Window("start|" + phase.Name);
            var thread = Environment.CurrentManagedThreadId;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < count; i++) phase.Action();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (thread != Environment.CurrentManagedThreadId) throw new InvalidOperationException("Allocation window crossed threads.");
            Markers.Log.Window(FormattableString.Invariant($"end|{phase.Name}|{allocated}|{count}"));
            Console.WriteLine($"{phase.Name}: {allocated / (double)count / 1048576:F2} MiB managed/request ({count} requests)");
        }
    }

    private static void Draw(Style style, ICanvas canvas)
    {
        using var lifetime = canvas as IDisposable;
        using var bitmap = Renderer.Render(style, canvas, 8580, 10646, 14, 512, 512).GetAwaiter().GetResult();
        if (bitmap == null) throw new InvalidOperationException("Render did not complete.");
    }

    public static void TimePreparation(string root, string output, int iterations)
    {
        if (iterations < 10) throw new ArgumentOutOfRangeException(nameof(iterations));
        using var source = new SingleMbTilesSource(Path.Combine(root, "tiles", "zurich.mbtiles"));
        var tile = source.GetVectorTile(8580, 10646, 14).GetAwaiter().GetResult();
        if (tile == null) throw new InvalidOperationException("Required native tile is missing.");
        var style = new Style(Path.Combine(root, "styles", "bright-style.json"))
        {
            FontDirectory = Path.Combine(root, "styles", "fonts")
        };
        style.SetSourceProvider(0, source);
        Renderer.ProfileSink = null;
        Renderer.CacheProfileSink = null;
        var warmup = System.Diagnostics.Stopwatch.StartNew();
        do
        {
            Draw(style, new NoDrawCanvas());
        }
        while (warmup.Elapsed < TimeSpan.FromSeconds(1));
        var samples = new List<object>();
        for (var i = 0; i < iterations; i++)
        {
            var thread = Environment.CurrentManagedThreadId;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            Draw(style, new NoDrawCanvas());
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (thread != Environment.CurrentManagedThreadId) throw new InvalidOperationException("Preparation crossed threads.");
            samples.Add(new { Iteration = i, ElapsedMs = elapsed, ManagedBytes = allocated });
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            LibrarySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(Style).Assembly.Location))),
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            Iterations = iterations, MinimumWarmupMs = 1000, Tile = "zurich-z14-8580-10646", Size = 512,
            Policy = "Decoded tile reused, observers off, no-op drawing backend with 1x1 bitmap. Includes layer preparation, ordering and no-op dispatch/disposal. Excludes real rasterization, PNG encoding and source decode. No forced GC. Attribution experiment requires separate exact-pixel/full-request checks.",
            Samples = samples
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(output);
    }

    public static void Report(string tracePath, string output)
    {
        var etlx = TraceLog.CreateFromEventPipeDataFile(Path.GetFullPath(tracePath), Path.GetFullPath(output) + ".etlx");
        using var log = new TraceLog(etlx);
        using var source = log.Events.GetSource();
        var samples = new Dictionary<(string Phase, string Type, string Stack), (long Weight, int Count)>();
        var totals = new List<object>();
        string current = null;
        var thread = -1;
        source.Dynamic.All += data =>
        {
            if (data.ProviderName != "VectorTileRenderer-Allocation") return;
            var fields = ((string)data.PayloadByName("value")).Split('|');
            if (fields[0] == "start")
            {
                current = fields[1];
                thread = data.ThreadID;
            }
            else if (fields[0] == "end")
            {
                if (current != fields[1]) throw new InvalidOperationException("Mismatched allocation markers.");
                totals.Add(new { Phase = current, ManagedBytes = long.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture),
                    Iterations = int.Parse(fields[3], System.Globalization.CultureInfo.InvariantCulture) });
                current = null;
            }
        };
        source.Clr.GCAllocationTick += data =>
        {
            if (current == null || data.ThreadID != thread) return;
            var frames = new List<string>();
            for (var frame = log.GetCallStackForEvent(data); frame != null && frames.Count < 30; frame = frame.Caller)
                frames.Add(frame.CodeAddress.FullMethodName);
            var key = (current, data.TypeName, string.Join(" <- ", frames));
            var previous = samples.GetValueOrDefault(key);
            samples[key] = (previous.Weight + data.AllocationAmount64, previous.Count + 1);
        };
        source.Process();
        if (totals.Count < 3 || samples.Count == 0)
        {
            Console.WriteLine($"Completed windows: {totals.Count}, sample groups: {samples.Count}");
            foreach (var data in log.Events.Where(data => data.ProviderName == "VectorTileRenderer-Allocation"))
                Console.WriteLine($"Marker {data.ID} {data.EventName}: {data}");
            foreach (var group in log.Events.GroupBy(data => data.ProviderName + "/" + data.EventName).OrderByDescending(group => group.Count()).Take(20))
                Console.WriteLine($"{group.Key}: {group.Count()}");
            throw new InvalidOperationException("Trace is missing allocation windows or samples.");
        }
        var types = samples.GroupBy(pair => (pair.Key.Phase, pair.Key.Type))
            .Select(group => new { group.Key.Phase, group.Key.Type, SampledWeightBytes = group.Sum(pair => pair.Value.Weight),
                Samples = group.Sum(pair => pair.Value.Count) }).OrderByDescending(row => row.SampledWeightBytes).ToArray();
        var stacks = samples.OrderByDescending(pair => pair.Value.Weight).Select(pair => new
        {
            pair.Key.Phase, pair.Key.Type, pair.Key.Stack, SampledWeightBytes = pair.Value.Weight, Samples = pair.Value.Count
        }).ToArray();
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Trace = Path.GetFullPath(tracePath), EventsLost = log.EventsLost,
            Interpretation = "GC allocation ticks are samples; interval weight attributed to sampled type/stack is approximate, not an exact type census. Current-thread counters are exact managed allocation totals for marked windows; no native-memory or latency claim.",
            Totals = totals, Types = types, Stacks = stacks
        }, new JsonSerializerOptions { WriteIndented = true }));
        foreach (var phase in types.GroupBy(row => row.Phase))
        {
            Console.WriteLine(phase.Key);
            foreach (var row in phase.Take(10)) Console.WriteLine($"  {row.Type}: {row.SampledWeightBytes / 1048576.0:F1} MiB interval weight, {row.Samples} ticks");
        }
    }

    // Preparation attribution only: retain layer preparation while removing backend drawing.
    private sealed class NoDrawCanvas : ICanvas
    {
        public bool ClipOverflow { get; set; }
        public void StartDrawing(double x, double y) { }
        public void DrawBackground(Brush brush) { }
        public void DrawLineString(List<Point> points, Brush brush) { }
        public void DrawPolygon(List<Point> points, Brush brush) { }
        public void DrawPoint(Point point, Brush brush) { }
        public void DrawText(Point point, Brush brush) { }
        public void DrawTextOnPath(List<Point> points, Brush brush) { }
        public void DrawImage(Stream stream, Brush brush) { }
        public void DrawUnknown(List<List<Point>> points, Brush brush) { }
        public SKBitmap FinishDrawing() => new(1, 1);
    }
}
