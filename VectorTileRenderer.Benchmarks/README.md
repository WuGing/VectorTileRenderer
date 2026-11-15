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
layer preparation with a no-op drawing backend. The latter is allocation attribution
only, not a renderer output or latency benchmark. Both profiling callbacks are off;
no PNG encoding or pixel-array checks occur inside the marked windows.

The analyzer uses the TraceEvent version already included by BenchmarkDotNet. It
groups allocation ticks by marked phase, allocating thread, sampled type and stack,
reports exact current-thread managed-byte counters from markers, and records lost
events. Sample interval weights are approximate type/stack attribution, not exact
object-byte totals. Native memory, retained heap/leaks and application latency are
not measured. Run latency comparisons without the allocation collector. JSON/ETLX
and raw traces are local ignored artifacts; ETLX caches can be large.
