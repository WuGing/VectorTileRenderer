# Style evaluation allocation reduction — 2026-11-15

## Result

Allocation tracing found a captured evaluator closure in
[Style.GetValue](../../VectorTileRenderer/Style.cs), invoked by filters and style
properties. Replacing its capturing array Select projection with a recursive indexed
loop removes the closure allocation from scalar evaluations as well. Empty arrays
still return an empty object array; input arrays are not mutated. No style semantics,
public signatures, caching policy or rendering algorithm intentionally changed.

Exact managed allocation totals per request, profiling callbacks disabled:

| Diagnostic phase | Before MiB | After MiB | Reduction |
|---|---:|---:|---:|
| Full warm CPU render | 18.85 | 14.08 | 25.3% |
| Warm layer preparation, drawing disabled | 15.01 | 10.25 | 31.8% |
| Fresh native tile decode | 30.58 | 30.58 | None |

This is an allocation-pressure improvement, **not a demonstrated latency win**.
An untraced focused run measured 55.35 → 54.69 ms median warm rendering; that small
difference is insufficient for a meaningful speedup claim. Existing direct PNG gains
remain a separate [measured result](Direct-Png-2026-11-15.md).

## Investigation and evidence

Host/runtime/dataset match the [CPU workload baseline](Request-Workloads-2026-11-15.md):
i7-13700K, Windows 11 Pro 10.0.26200, SDK 10.0.400, .NET 10.0.11 x64. Bright style,
native Zurich TMS z14/8580/10646, 512 pixels. Baseline 8e53adf plus the uncommitted
direct-PNG/workload changes. Only the GetValue array branch changes production code
in this allocation slice.

New [AllocationInvestigation](../../VectorTileRenderer.Benchmarks/AllocationInvestigation.cs)
adds marked workloads and an offline report to the benchmark executable. Five
warmups per operation, then 100 warm renders, 100 fresh decodes and 100 layer-build
requests with a no-op drawing backend. The last phase is attribution only; it returns
a dummy bitmap and is not a renderer correctness/performance result. No PNG encoding,
pixel-array verification or profiling callbacks inside allocation windows. Synchronous
requests and current-thread counters are guarded against thread changes. Native
memory, retained heap/leaks and process-wide allocation totals are not measured.

dotnet-trace 10.0.750501 was installed locally under ignored artifacts/diagnostics-tools.
The analyzer uses TraceEvent 3.1.21 already present transitively through BenchmarkDotNet;
no diagnostics dependency was added to the reusable library. It resolves marked
allocation ticks to allocating-thread types and stacks using ETLX rundown data.
Allocation tick interval weights are approximate attribution, not exact per-type
allocation totals. See Microsoft's
[allocation event description](https://learn.microsoft.com/en-us/dotnet/fundamentals/diagnostics/runtime-garbage-collection-events)
and [gc-verbose profile](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace).

The valid before/after traces each reported **zero lost events**. Before, the top
warm-build sampled type was `<>c__DisplayClass27_0`, about 444 MiB of interval weight
over 100 requests. Resolved stacks locate it at GetValue, called from ValidateUsingFilter
and ValidateLayer. Source confirms the lambda captures attributes in the array branch;
the compiler creates the closure even when a call handles a scalar. After the loop
replacement, no captured-display-class samples containing GetValue remain. Exact
byte-counter reductions above establish the effect independently of sample weighting.

Remaining warm-build sample leaders are attribute dictionaries/entry arrays, Paint,
localized Point arrays and visual layers. Fresh decode sample leaders include UInt32
arrays, dictionaries/entry arrays, Mapbox coordinate arrays and parser iterators.
These are follow-up leads, not justification for changing ownership or parser behavior
without targeted tests and measurements. Fresh decode remained unchanged in this fix.

The first capture had Begin markers but no completion markers, so it was excluded
from allocation attribution. Replacing them with explicitly informational string
markers produced complete windows. Superseded ETLX debug caches were removed; valid
raw traces and reports remain local.

## Regression, output and timing checks

[StyleTests](../../VectorTileRenderer.Tests/StyleTests.cs) reproduced **240,000 bytes**
for 10,000 scalar filter evaluations before the fix. Afterward the allocation-budget
regression passes (<4,096 bytes total). Another regression verifies attribute-driven
array values across different inputs and confirms the source array is unchanged.

Focused untraced before/after timing uses the existing seven-mode workload, 50
measurements after five warmups per mode. Total/allocations include the established
request boundaries; both profiling callbacks are enabled here, unlike the trace
windows. That explains the approximately 0.64 MiB difference from marked counters.

| Mode | Before median ms | After median ms | Before managed MiB | After managed MiB |
|---|---:|---:|---:|---:|
| Render warm | 55.35 | 54.69 | 19.490 | 14.723 |
| Render fresh | 96.92 | 93.87 | 50.073 | 45.306 |
| Miss warm | 89.74 | 89.53 | 19.495 | 14.728 |
| Miss fresh | 129.56 | 125.08 | 50.078 | 45.312 |
| Bitmap hit | 3.13 | 3.12 | 0.012 | 0.012 |
| Miss warm + caller PNG | 122.35 | 119.30 | 19.866 | 15.099 |
| Hit + caller PNG | 31.54 | 31.42 | 0.383 | 0.383 |

The 4.77 MiB allocation saving is repeatable across the relevant modes; elapsed-time
changes are small and host activity/OS caches/clock state remain uncontrolled.
The production direct-PNG path also uses the same evaluator on misses, but this table
does not claim a newly measured direct-PNG latency benefit.

A separate eight-case correctness run (Basic/Bright, z12/z14, 256/512, ten measured
rounds per mode) passed exact per-request/publication checks. All eight saved reference
PNGs matched the earlier full baseline **byte-for-byte**, and the focused before/after
reference PNGs share SHA-256
`DE9B69C1291087EA8F44393BF1E5BD579AFDEF4480DE7570F51009B923A79B6B`.
No new sample assets or rendering changes were committed.

- Full restore-enabled Release solution/four-target build: zero warnings/errors.
- Regression suite: **118 passed, zero skips**.
- Library and symbols packaging passed to ignored artifacts/style-allocation-package.
- GitNexus exploring used; GetValue upstream impact HIGH, ParseStyle and filter
  evaluation source-verified. Index baseline current but prior uncommitted/new-symbol
  coverage incomplete; FTS query remains degraded. Full regression/build plus eight
  pixel cases address the shared evaluator risk. No public contract change.
- PNG API/shared-helper review found no blocking issue in the reviewed scope; no new
  interactive Mapsui smoke test or GPU run is claimed in this allocation slice.

## Reproduction and local evidence

See [benchmark instructions](../../VectorTileRenderer.Benchmarks/README.md) for optional
local tool installation and directory setup.

```powershell
artifacts/diagnostics-tools/dotnet-trace collect --profile gc-verbose --providers VectorTileRenderer-Allocation --output artifacts/allocation-trace/run.nettrace --show-child-io -- dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --allocation-workload . 100
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --allocation-report artifacts/allocation-trace/run.nettrace artifacts/allocation-trace/run.json
```

Local evidence:

- Before [resolved allocation report](../../artifacts/allocation-trace/report.json)
  and raw artifacts/allocation-trace/allocations2.nettrace.
- After [resolved report](../../artifacts/allocation-trace/after.json) and raw
  artifacts/allocation-trace/after.nettrace.
- [Untraced timing/managed-byte CSV](../../artifacts/allocation-trace/timing-comparison.csv).
- Before artifacts/style-closure-before/20261115-201622-37f89222/results.json;
  after artifacts/style-closure-after/20261115-201803-d27cf34b/results.json;
  wide pixel check artifacts/style-closure-pixel-check/20261115-202046-8143a485/results.json.

Next review/checkpoint the accumulated PNG and allocation work. Further style/build
or decoder changes need their own targeted allocation and latency evidence. Keep
provider reuse, ownership, PNG recovery and existing GPU parity gates intact. R-006
remains In progress; no version bump, commit, push or publication in this slice.
