using System.Diagnostics.Tracing;
using System.Linq;
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

    public static void Run(string[] args)
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
        foreach (var action in new[] { decode, render, build })
            for (var i = 0; i < 5; i++) action();
        foreach (var phase in new[] { (Name: "warm-render", Action: render), (Name: "fresh-decode", Action: decode), (Name: "warm-build-no-draw", Action: build) })
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
        if (totals.Count != 3 || samples.Count == 0)
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

    // Allocation attribution only: retain Renderer layer preparation while removing backend drawing.
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
