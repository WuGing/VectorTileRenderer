# VectorTileRenderer benchmarks

This project is intentionally separate from the test suite. It contains current
implementations alongside local legacy baselines so alternative implementations
can be measured before they are moved into the reusable library.

Run all benchmarks:

```powershell
dotnet run --project VectorTileRenderer.Benchmarks -c Release
```

Run one benchmark group with a shorter exploratory job:

```powershell
dotnet run --project VectorTileRenderer.Benchmarks -c Release -- --filter "*QuadTree*" --job short
```

BenchmarkDotNet writes reports under `BenchmarkDotNet.Artifacts/`, which is
ignored by Git.

## Complete CPU tile requests

Run the sequential exploratory workload (independent of BenchmarkDotNet jobs):

```powershell
dotnet run --project VectorTileRenderer.Benchmarks -c Release -- --request-timing . artifacts/request-timing 50
```

Arguments after the flag are repository root, output parent and measured rounds
(minimum ten). A unique output subdirectory contains raw samples, median/p95 phase
summaries, host/runtime/dataset/style/font fingerprints and reference images.
An optional fourth argument selects one exact case name, for example
`bright-z14-8580-10646-512`, to check a noisy case in a fresh process.
The runner selects the largest compressed native tile at zooms 12 and 14 from the
existing Zurich MBTiles, ties by column/row, and uses Basic/Bright at 256/512 pixels.
These are stress samples, not a whole-map traffic distribution. Assets remain local;
see [licensing/provenance limits](../docs/test-data-licensing.md).

Seven modes rotate order for five warmups and then each measured round: direct
render with warm/fresh provider, PNG cache miss with warm/fresh provider, PNG cache
hit, and warm miss/hit followed by Mapsui-style PNG encoding to a byte array.
Fresh provider setup/metadata and disposal are included in external request time;
style is reused, canvas is new each request. OS/SQLite file caches are not cleared.
Misses use unique source revisions; hits use a seeded stable revision. Rendered
pixels must match a CPU reference exactly on every request and every published PNG;
actual CPU backend, declared hit/miss outcome, publication count and temp cleanup
are checked. Generated cache entries are pruned only after each case, outside timing.

External TotalMs includes setup, request, optional client PNG encoding and canvas/
provider disposal. It excludes returned bitmap disposal, pixel validation and report
writing. Current-thread managed allocations are guarded against thread transitions;
native allocations are not measured. GC counts are also recorded. Pixel verification
allocates outside the timers and can affect subsequent GC. Both profiling callbacks
are enabled. No UI queue, network or GPU-host measurement is implied. Phase medians
do not necessarily sum to the median total; fetch/style are nested in build time.

## Old/new PNG output comparison

```powershell
dotnet run --project VectorTileRenderer.Benchmarks -c Release -- --request-timing . artifacts/png-comparison 50 "*" png-comparison
```

The fifth argument selects six rotating modes: old bitmap API plus client PNG
encoding versus RenderCachedPng, each for warm misses, fresh misses and hits.
The fourth argument still filters an exact case; `*` includes all eight cases.
Both paths produce PNG bytes. This comparison includes old-path bitmap disposal
inside the timed request, matching Mapsui's lifetime, as well as the new API's
internal bitmap disposal. Every returned PNG and published file must decode to
the same CPU reference. Direct hits include strict full decode validation, not
just header checks. Existing baseline modes and BenchmarkDotNet discovery remain
available. Do not compare runs as if OS/GC/background activity were controlled.

## Marked allocation investigation

Install the optional tool locally (not into the library or global tool set):

```powershell
dotnet tool install dotnet-trace --tool-path artifacts/diagnostics-tools
```

Create an output directory, then collect and analyze:

```powershell
artifacts/diagnostics-tools/dotnet-trace collect --profile gc-verbose --providers VectorTileRenderer-Allocation --output artifacts/allocation-trace/run.nettrace --show-child-io -- dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --allocation-workload . 100
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --allocation-report artifacts/allocation-trace/run.nettrace artifacts/allocation-trace/run.json
```

The workload uses Bright/z14/8580/10646 at 512 pixels and five warmups. Marked
windows isolate 100 full warm renders, fresh native tile decodes and warm Renderer
layer preparation with a no-op drawing backend, plus external-parser-only and
adapter-conversion-only windows. The no-op phase is allocation attribution
only, not a renderer output or latency benchmark. Both profiling callbacks are off;
no PNG encoding or pixel-array checks occur inside the marked windows.

The analyzer uses the TraceEvent version already included by BenchmarkDotNet. It
groups allocation ticks by marked phase, allocating thread, sampled type and stack,
reports exact current-thread managed-byte counters from markers, and records lost
events. Sample interval weights are approximate type/stack attribution, not exact
object-byte totals. Native memory, retained heap/leaks and application latency are
not measured. Run latency comparisons without the allocation collector. JSON/ETLX
and raw traces are local ignored artifacts; ETLX caches can be large.

The parser window reads pre-decompressed bytes; the conversion window uses a
pre-parsed tile and a delegate bound once to the private adapter. Neither includes
the other stage, SQL metadata or filesystem cold-start costs. Older three-window
traces can still be analyzed. Full decode includes more than those isolated phases.

Capture exact decoded-model fingerprints before/after a conversion change:

```powershell
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --decode-snapshot . artifacts/allocation-trace/decode-snapshot.json
```

This snapshots the existing Zurich gzip/New York PBF and two native Zurich tiles.
Fingerprints include layer/feature and geometry order, normalized extent/geometry
type, sorted attribute keys with runtime value types/values, and coordinate double
bit patterns. Snapshot generation is outside measurements and does not replace
synthetic parser/ownership regressions.

## Parser thread profiling

### Path construction comparison

```powershell
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --path-construction-comparison . artifacts/allocation-trace/path-construction.json 50
```

Build Release and create the output parent first. This alternates legacy per-point
native calls and the batched builder on native Zurich z14 line/polygon geometry
localized to 512. Five warmups; each sample builds/disposes the full geometry set.
Fill type, verb counts and points are checked before timing. Managed counters
exclude native memory. Use complete requests/reference pixels to assess renderer
effects. See [drawing results, ownership tests and timing limits](../docs/02_Investigation/Path-Drawing-2026-11-15.md).

### Lossless PNG encoding comparison

```powershell
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --png-encoding-comparison . artifacts/allocation-trace/png-encoding-comparison.json 30
```

Build Release and create the output parent first. Nine encoder/filter modes rotate
on eight CPU-rendered map cases and three runtime-authored synthetic images. Five
warmups, requested sample count per mode/case. Every output is decoded and compared
with legacy decoded pixels after timing. Output includes exact byte sizes and
current-thread managed counters. Native result disposal/validation are outside
the encode timer. Run separately from complete-request timings and profilers.
See [measurements, raster policy and limits](../docs/02_Investigation/Png-Encoding-2026-11-15.md).

### Complete request profiling

```powershell
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --request-timing . artifacts/current-request-matrix 20 '*' png-comparison
artifacts/diagnostics-tools/dotnet-trace collect --profile dotnet-common,dotnet-sampled-thread-time --providers VectorTileRenderer-Request --output artifacts/allocation-trace/complete-request.nettrace --show-child-io -- dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --request-cpu-workload . artifacts/request-cpu-cache 150
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --request-cpu-report artifacts/allocation-trace/complete-request.nettrace artifacts/allocation-trace/complete-request.json
```

Build Release first and create the trace output parent. Run timings and traces
sequentially. The stack workload uses CPU Bright z14/8580/10646 at 512 with three
windows, fresh misses, reused-provider misses and hits. It disables renderer
observers, validates output/publication pixels after the windows, and prunes only
its generated PNGs. The report selects the marked OS thread, records lost events,
and reports inclusive/leaf stacks. Native codec internals remain unresolved and
sample shares describe thread wall time. See the
[current stage breakdown and two-capture evidence](../docs/02_Investigation/Complete-Request-Profile-2026-11-15.md).

### Direct raw-model conversion

```powershell
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --fused-comparison . artifacts/allocation-trace/fused-comparison.json 300
```

This alternates the attribute-optimized staged parser plus adapter with direct
raw-protobuf-to-renderer conversion, returning final renderer tiles from both.
The window includes standard protobuf parsing and geometry normalization, with
pre-decompressed native Zurich PBF, ten warmups and per-request managed counters.
Counts are checked after timing. Use decoded hashes and synthetic parity tests for
semantic validation. Create the output parent first. See
[evidence and limits](../docs/02_Investigation/Fused-Conversion-2026-11-15.md).

### Isolated protobuf reader

```powershell
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --protobuf-comparison . artifacts/allocation-trace/protobuf-isolated.json 300
```

This compares the current production adapter with the direct-append protobuf reader
under `ProtobufExperiment`. Both produce dependency decoded models. The experiment
has a private read-only model, and portable tests link its source directly. It
remains an experiment after complete-request measurements showed no useful gain.
See [results and limits](../docs/02_Investigation/Protobuf-Buffering-2026-11-15.md).

For parser comparisons against a saved library build, append its absolute assembly
path after the iteration count of `--parser-comparison`. A separate load context
loads that build while sharing dependency types. Output records baseline identity.

### Style evaluation comparison

Save the pre-change Release output before building the candidate, then run
separately from complete-request timing, builds and profilers.

```powershell
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --style-evaluation-comparison artifacts/style-eval-before-bin/WuGing.VectorTileRenderer.dll artifacts/style-eval-isolated.json 50
```

This loads the saved assembly into a separate context and alternates baseline and
candidate delegates over identical mutable inputs. Each sample performs 100,000
literal-string, numeric-filter, nested-filter or three-stop evaluations. Ten
additional warmups follow at least one second of alternating warmup per workload.
Results and managed bytes exclude rendering and
source loading. Synthetic gains require complete-request and pixel validation.
See [results](../docs/02_Investigation/Style-Evaluation-2026-11-15.md).

### Alternating complete-request and preparation comparisons

When host variation affects separate processes, load a saved baseline beside the
candidate and alternate complete direct-PNG requests in one process.

```powershell
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --paired-request-comparison . artifacts/preparation-before-bin/WuGing.VectorTileRenderer.dll artifacts/preparation-paired.json 60
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --preparation-timing . artifacts/preparation-isolated-after.json 100
```

The paired command rotates fresh-provider misses, reused-provider misses and cache
hits, alternating implementation order each round. Five warmups precede each mode.
Styles/providers/caches belong to their implementation. Reflection/setup/disposal,
encoding and publication are timed in both paths. Observers are off. Every output
and publication is decoded and compared exactly to the baseline CPU reference
outside timers. Output includes assembly hashes and current-thread managed bytes.

Preparation attribution uses a warmed decoded tile and the existing no-op canvas,
with at least one second of warmup. It includes layer preparation, ordering and
no-op draw dispatch with a 1x1 output bitmap. To measure the baseline, copy the
current benchmark output and replace only its renderer library with the saved
baseline library. Output records the loaded library hash. This isolates preparation
costs and requires separate real-rendering pixel and complete-request validation.
Run all measurements separately from builds, tests and profiling.
See [preparation results](../docs/02_Investigation/Filter-Preparation-2026-11-15.md).

### Line/polygon submission census and replay

```powershell
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --submission-census . artifacts/submission-census.json
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --submission-timing . artifacts/submission-replay-after.json 50
```

The census renders eight CPU map cases and counts renderer line/polygon submissions
by geometry-list identity. Replay records the heavy Bright z14/512px commands, then
draws only those line/polygon commands in original order on fresh CPU canvases.
At least one second of warmup precedes timing. Surface setup, path construction,
submission, finish and canvas disposal are included. Source decoding, preparation,
point/text drawing and PNG encoding are excluded. Pixels are checked after each
sample outside timing, with the reference pixel hash recorded for cross-build
comparison. The baseline uses an identical benchmark build with only the renderer
library replaced by the saved pre-change library. Complete-request paired runs and
the eight-case pixel matrix provide the real-request compatibility/performance checks.
See [results and rejected cache experiment](../docs/02_Investigation/Path-Submission-2026-11-15.md).

### Fresh fetching and decode attribution

```powershell
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --fresh-decode-timing . artifacts/fresh-decode-final-after.json 100
```

This rotates provider construction/metadata, warmed SQLite raw reads, buffer-based
gzip inflation, raw protobuf model parsing, dependency geometry parsing, full plain/
gzip renderer decoding and fresh-provider decoding. Ten warmups precede 100 samples
per operation. Stages overlap. The inflate pilot materializes a buffer while the
production loader streams gzip. Output records the library hash and managed bytes.
Use an identical benchmark build with only its library replaced for baseline runs.
Complete paired requests, typed snapshots and the eight-case pixel matrix validate
the production result. See [fresh-decode results](../docs/02_Investigation/Fresh-Decode-2026-11-15.md).

### Current profiling windows

The parser-only CPU/allocation window profiles the external dependency parser.
Fresh-decode exercises the current production loader. To compare the internal
adapter with the dependency, use alternating unprofiled parser requests:

```powershell
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --parser-comparison . artifacts/allocation-trace/attribute-parser-comparison.json 300
```

This loads pre-decompressed Zurich z14/8580/10646 PBF once, runs ten warmups per
implementation, alternates order and records elapsed time/current-thread bytes.
Create the output parent first. Per-request layer/feature counts are checked after
timing. Use decoded fingerprints and synthetic parity tests for semantic validation.

Run separately from allocation capture, builds, tests and unprofiled timing:

```powershell
dotnet build VectorTileRenderer.Benchmarks/VectorTileRenderer.Benchmarks.csproj -c Release
artifacts/diagnostics-tools/dotnet-trace collect --profile dotnet-common,dotnet-sampled-thread-time --providers VectorTileRenderer-Allocation --output artifacts/allocation-trace/parser-cpu.nettrace --show-child-io -- dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --parser-cpu-workload . 1000
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --parser-cpu-report artifacts/allocation-trace/parser-cpu.nettrace artifacts/allocation-trace/parser-cpu.json
```

Requires a separately installed local dotnet-trace tool. Create the output parent
directory first. Workload uses parser-only, conversion-only and fresh-decode
windows. Report requires all three windows and sample stacks, retains lost-event
count, and groups parser samples with GC-poll precedence. Counts estimate managed
thread wall time. Inclusive methods overlap, unresolved/native/inlined frames limit
leaf attribution, and excluding GC-poll samples does not establish exact CPU time.
See the [two-capture evidence](../docs/02_Investigation/Parser-Cpu-2026-11-15.md).
