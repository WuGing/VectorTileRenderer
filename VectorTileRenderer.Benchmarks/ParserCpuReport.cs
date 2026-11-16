using System.Text.Json;
using System.Linq;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace VectorTileRenderer.Benchmarks;

internal static class ParserCpuReport
{
    public static void Run(string tracePath, string output, string markerProvider = "VectorTileRenderer-Allocation")
    {
        var etlx = TraceLog.CreateFromEventPipeDataFile(Path.GetFullPath(tracePath), Path.GetFullPath(output) + ".etlx");
        using var log = new TraceLog(etlx);
        using var source = log.Events.GetSource();
        var stacks = new Dictionary<(string Phase, string Stack), int>();
        var windows = new List<object>();
        string current = null;
        var thread = -1;
        var started = 0.0;
        source.AllEvents += data =>
        {
            if (data.ProviderName == markerProvider)
            {
                var fields = ((string)data.PayloadByName("value")).Split('|');
                if (fields[0] == "start")
                {
                    current = fields[1];
                    thread = data.ThreadID;
                    started = data.TimeStampRelativeMSec;
                }
                else if (fields[0] == "end")
                {
                    if (current != fields[1] || thread != data.ThreadID)
                        throw new InvalidOperationException("Mismatched workload window.");
                    windows.Add(new { Phase = current, ElapsedMs = data.TimeStampRelativeMSec - started,
                        ManagedBytes = long.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture),
                        Iterations = int.Parse(fields[3], System.Globalization.CultureInfo.InvariantCulture) });
                    current = null;
                }
                return;
            }
            if (current == null || data.ThreadID != thread || data.ProviderName != "Microsoft-DotNETCore-SampleProfiler") return;
            var frames = new List<string>();
            for (var frame = log.GetCallStackForEvent(data); frame != null; frame = frame.Caller)
                frames.Add(frame.CodeAddress.FullMethodName);
            if (frames.Count == 0) frames.Add("[missing stack]");
            var key = (current, string.Join(" <- ", frames));
            stacks[key] = stacks.GetValueOrDefault(key) + 1;
        };
        source.Process();
        if (windows.Count != 3 || stacks.Count == 0)
            throw new InvalidOperationException("Expected three completed windows and thread samples.");
        var rows = stacks.Select(pair => new { pair.Key.Phase, pair.Key.Stack, Samples = pair.Value }).ToArray();
        var methods = rows.SelectMany(row => row.Stack.Split(" <- ").Distinct().Select(method => new { row.Phase, Method = method, row.Samples }))
            .GroupBy(row => (row.Phase, row.Method))
            .Select(group => new { group.Key.Phase, group.Key.Method, InclusiveSamples = group.Sum(row => row.Samples) })
            .OrderByDescending(row => row.InclusiveSamples).ToArray();
        var leaves = rows.GroupBy(row => (row.Phase, Method: row.Stack.Split(" <- ")[0]))
            .Select(group => new { group.Key.Phase, group.Key.Method, LeafSamples = group.Sum(row => row.Samples) })
            .OrderByDescending(row => row.LeafSamples).ToArray();
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Trace = Path.GetFullPath(tracePath), EventsLost = log.EventsLost, Windows = windows,
            Interpretation = "Sampled target-thread wall time. Inclusive counts overlap. Leaf counts include unresolved runtime/native addresses and JIT inlining effects. No exact CPU-time or unprofiled latency claim.",
            Totals = rows.GroupBy(row => row.Phase).Select(group => new { Phase = group.Key, Samples = group.Sum(row => row.Samples) }),
            ParserGroups = rows.Where(row => row.Phase == "parser-only")
                .GroupBy(row => row.Stack.Contains("PollGC", StringComparison.Ordinal) ? "GC poll on target thread"
                    : row.Stack.Contains("AttributesParser", StringComparison.Ordinal) ? "Attributes"
                    : row.Stack.Contains("GeometryParser", StringComparison.Ordinal) ? "Geometry"
                    : row.Stack.Contains("ProtoBuf", StringComparison.Ordinal) ? "Protobuf"
                    : "Other parser/runtime")
                .Select(group => new { Group = group.Key, Samples = group.Sum(row => row.Samples) }),
            Methods = methods, Leaves = leaves, Stacks = rows.OrderByDescending(row => row.Samples)
        }, new JsonSerializerOptions { WriteIndented = true }));
        foreach (var phase in methods.GroupBy(row => row.Phase))
        {
            Console.WriteLine(phase.Key);
            foreach (var row in phase.Take(18)) Console.WriteLine($"  {row.InclusiveSamples}: {row.Method}");
        }
    }
}
