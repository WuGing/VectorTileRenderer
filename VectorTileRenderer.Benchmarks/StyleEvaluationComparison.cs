using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace VectorTileRenderer.Benchmarks;

internal static class StyleEvaluationComparison
{
    public static void Run(string baselineAssembly, string output, int iterations)
    {
        if (iterations < 10) throw new ArgumentOutOfRangeException(nameof(iterations));
        var context = new AssemblyLoadContext("style-evaluation-baseline", isCollectible: true);
        try
        {
            var baseline = context.LoadFromAssemblyPath(Path.GetFullPath(baselineAssembly))
                .GetType(typeof(Style).FullName);
            var baselineValue = baseline.GetMethod("GetValue", BindingFlags.NonPublic | BindingFlags.Static)
                .CreateDelegate<Func<object, Dictionary<string, object>, object>>();
            var candidateValue = typeof(Style).GetMethod("GetValue", BindingFlags.NonPublic | BindingFlags.Static)
                .CreateDelegate<Func<object, Dictionary<string, object>, object>>();
            var baselineFilter = baseline.GetMethod("ValidateUsingFilter", BindingFlags.NonPublic | BindingFlags.Static)
                .CreateDelegate<Func<object[], Dictionary<string, object>, bool>>();
            var candidateFilter = typeof(Style).GetMethod("ValidateUsingFilter", BindingFlags.NonPublic | BindingFlags.Static)
                .CreateDelegate<Func<object[], Dictionary<string, object>, bool>>();
            var attributes = new Dictionary<string, object>
            {
                ["class"] = "major", ["rank"] = 7L, ["$zoom"] = 7.5d
            };
            object[] numeric = [">=", "rank", 4d];
            object[] recursive = ["all", new object[] { "==", "class", "major" },
                new object[] { "any", new object[] { "<", "rank", 2d }, numeric },
                new object[] { "!in", "class", "minor", "service" }];
            var stops = new Dictionary<string, object>
            {
                ["stops"] = new object[] { new object[] { 5d, 2d }, new object[] { 10d, 10d }, new object[] { 15d, 20d } }
            };
            const int calls = 100000;
            var workloads = new (string Name, Func<int> Before, Func<int> After)[]
            {
                ("literal-string", () => Values(baselineValue, "major"), () => Values(candidateValue, "major")),
                ("numeric-filter", () => Filters(baselineFilter, numeric), () => Filters(candidateFilter, numeric)),
                ("recursive-filter", () => Filters(baselineFilter, recursive), () => Filters(candidateFilter, recursive)),
                ("zoom-stops", () => Values(baselineValue, stops), () => Values(candidateValue, stops))
            };
            var samples = new List<object>();
            foreach (var workload in workloads)
            {
                if (workload.Before() != workload.After()) throw new InvalidOperationException("Evaluation differs.");
                var warmup = Stopwatch.StartNew();
                do
                {
                    workload.Before();
                    workload.After();
                }
                while (warmup.Elapsed < TimeSpan.FromSeconds(1));
                for (var round = -10; round < iterations; round++)
                for (var offset = 0; offset < 2; offset++)
                {
                    var candidate = (round + 10 + offset) % 2 == 0;
                    var evaluate = candidate ? workload.After : workload.Before;
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    var started = Stopwatch.GetTimestamp();
                    var result = evaluate();
                    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                    if (result != calls) throw new InvalidOperationException("Unexpected evaluation result.");
                    if (round >= 0) samples.Add(new { Workload = workload.Name, Round = round,
                        Mode = candidate ? "candidate" : "baseline", ElapsedMs = elapsed, ManagedBytes = allocated });
                }
            }
            File.WriteAllText(output, JsonSerializer.Serialize(new
            {
                Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                BaselineAssembly = Path.GetFullPath(baselineAssembly), Iterations = iterations,
                WarmupMinimumMsPerWorkload = 1000, AdditionalWarmupRounds = 10,
                CallsPerSample = calls, ResultEquality = true,
                Policy = "Synthetic scalar, numeric, nested-filter and three-stop workloads. Alternating order, identical mutable inputs, static delegates, no profiler or forced GC. Excludes rendering and source loading.",
                Samples = samples
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(output);

            int Values(Func<object, Dictionary<string, object>, object> evaluate, object token)
            {
                var expected = candidateValue(token, attributes);
                var accepted = 0;
                for (var i = 0; i < calls; i++) if (Equals(evaluate(token, attributes), expected)) accepted++;
                return accepted;
            }

            int Filters(Func<object[], Dictionary<string, object>, bool> evaluate, object[] filter)
            {
                var accepted = 0;
                for (var i = 0; i < calls; i++) if (evaluate(filter, attributes)) accepted++;
                return accepted;
            }
        }
        finally
        {
            context.Unload();
        }
    }
}
