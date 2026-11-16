# Complete request profile after preparation changes — 2026-11-15

Inspected `f79f882` plus pending [filter/preparation changes](Filter-Preparation-2026-11-15.md)
and preceding performance work. **Drawing is the largest observed group in both
observer-free stack captures**, approximately 42% fresh / 53% reused-provider.
Instrumented stage timers keep preparation and fresh decoding close. Line/polygon
submission and repeated path construction are the next shared investigation target.

## Workload and correctness

Windows 11 Pro 26200, i7-13700K, 24 logical processors, approximately 31.3 GiB RAM,
SDK 10.0.400, .NET 10.0.11 x64, Skia assembly 3.119.0.0 CPU. Checked-in Zurich
MBTiles, Basic/Bright styles and repository fonts. Native TMS z12/2145/2662 and
z14/8580/10646, 256/512 pixels. OS filesystem caches are warmed/uncontrolled.
UTC artifact timestamps fall on November 16, local November 15.

The eight-case matrix runs six rotating modes, 20 samples/mode, followed by
50 samples/mode on heavy Bright z14 at 512px. Five warmups/mode. All 1,260 timed
outputs and cache publications pass exact CPU pixel checks. All eight reference
PNG hashes match the preceding preparation checkpoint. Timings include setup,
request, optional client encoding and disposal, with render/cache observers enabled.
Validation is outside timers and may influence later GC.

Two independent traces each run 150 fresh misses, 150 reused-provider misses and
150 cache hits, observers disabled inside marked windows. Final outputs and all
622 published PNGs match their CPU reference. Both captures report zero lost events.
Fresh/warm/hit sample counts are 4,107/3,131/176 and 4,170/3,168/176.
Builds, timings and traces run sequentially. No forced GC.

## Instrumented complete-request timing

Heavy direct-PNG matrix/repeat medians are 130.69/78.38 ms fresh, 73.01/53.99 ms
reused-provider miss, and 3.05/2.38 ms hit. The matrix's heavy case is visibly
slower across all modes. Repeat p95 is 97.48 ms fresh, 70.27 ms warm and 2.97 ms
hit. These runs prioritize stages. Host/GC variation limits comparisons with older
profiles and supplies no new paired speedup estimate.

Disjoint stage durations are per-sample means from the focused repeat. Other
preparation is BuildMs minus FetchMs minus StyleMs. Shares add to external total.

| Stage | Fresh mean ms | Share | Reused-provider mean ms | Share |
|---|---:|---:|---:|---:|
| Geometry and text drawing | 24.54 | 31.2% | 23.01 | 41.4% |
| Fetch/decode | 23.69 | 30.1% | <0.01 | <0.1% |
| Preparation and style | 22.33 | 28.4% | 25.01 | 45.0% |
| PNG encoding/publication | 7.30 | 9.3% | 7.26 | 13.1% |
| Setup, lookup, finish and remainder | 0.81 | 1.0% | 0.31 | 0.6% |
| Total | 78.68 | 100% | 55.59 | 100% |

Preparation comprises timed style/filter work 8.23 ms fresh / 8.37 ms warm and
other preparation 14.10/16.64 ms. Drawing comprises geometry 16.75/16.17 ms and
text 7.79/6.83 ms. Other preparation includes layer construction, attribute/
geometry work, ordering and profiling counters. Its full duration cannot be
assigned to a single operation from these timers.

Fresh drawing, decode and preparation are closely sized. Warm preparation leads
the instrumented timer grouping. Per-feature timing/counter observers add work
during preparation and can affect this ranking. The observer-free stack evidence
below identifies drawing as the strongest common next target.

Cache hits average 2.53 ms, 99.4% in lookup/full-image validation. Trace-window
managed allocation is 27.26 MiB fresh, 9.22 MiB warm and 0.32 MiB hit per request.
These counters exclude native and retained memory.

## Observer-free stack evidence

Shares describe sampled target-thread wall time. Inclusive method entries overlap,
with recursive occurrences counted once per sample. Native internals, JIT inlining
and runtime polling limit attribution.

| Entry/group | Fresh share | Warm share |
|---|---:|---:|
| Drawing-entry union | 41.8–42.5% | 53.2–53.5% |
| Filter/value/style-entry union | 17.3–17.4% | 23.2–23.4% |
| PbfParser.ParseRendererTile | 19.9–20.9% | |
| SkiaCanvas.DrawLineString | 17.8–17.9% | 22.8–23.2% |
| SkiaCanvas.DrawPolygon | 11.7–12.1% | 14.0–14.2% |
| SkiaCanvas.GetPathFromGeometry | 6.3–7.1% | 7.4–8.0% |
| Combined text/path-text drawing | 12.3–12.5% | 16.2–16.3% |
| Style.ValidateUsingFilter | 16.4–16.7% | 22.2–22.5% |
| Style.GetValue | 14.9–15.3% | 20.8–21.5% |
| Style.ParseStyle | 0.6–0.9% | 0.8–1.0% |
| Renderer.EncodeCachePng | 11.1–11.2% | 14.6–14.9% |
| Renderer.PublishPng | 2.2–2.4% | 2.8–2.9% |
| Renderer.LocalizeGeometry | 0.1–0.2% | 0.8% |

The drawing union includes DrawLineString, DrawPolygon, DrawPoint, DrawText and
DrawTextOnPath, counting each matching stack once. The style union includes
GetValue, ValidateUsingFilter and ParseStyle, also counting once per stack. These
unions avoid adding overlapping entries. Their shares cover only those entry
frames, leaving other renderer preparation in the remainder.

GC-poll frames occur in approximately 23% of fresh/warm samples. They provide no
exact GC-worker CPU or pause-duration budget. Line/polygon entries include native
rasterization and child path work. Brush creation now has a small named ParseStyle
share. Filtering remains substantial alongside drawing.

## Source interpretation and next target

[SkiaCanvas.DrawLineString/DrawPolygon](../../VectorTileRenderer/SkiaCanvas.cs)
construct a path for every draw and submit it to SKCanvas.DrawPath. Paths already
use batched AddPoly and deterministic disposal, and paints are reused per canvas.
[Renderer.Render](../../VectorTileRenderer/Renderer.cs) caches localized geometry
per feature/request and can submit that geometry under multiple visual layers.

Next isolate line/polygon path construction and native drawing/submission costs.
Request-local reuse of paths for repeated geometry is a candidate requiring
measurement of actual repetition and native lifetime. Preserve draw order,
clipping, geometry mutation behavior and exact CPU pixels. The current captures
do not isolate native rasterizer subroutines or prove how much path reuse saves.
Fresh decoding remains a closely sized lead for new tiles. PNG has a smaller
timed share, and cache hits are dominated by full decode validation.

GitNexus reports the committed index current at HEAD. Concept query returns no
flows with `FTS indexes missing — keyword search degraded. Run: gitnexus analyze
--force to rebuild indexes.` Exact file-qualified DrawLineString context identifies
Renderer.Render, clipping and GetPathFromGeometry. Its indexed paint construction
predates pending changes, so direct source and runtime captures establish current
behavior. Dirty additions are outside the indexed snapshot.

This slice updates measurements/docs. Benchmark build passes. The preceding
implementation checkpoint passed 190 Release tests, restore-enabled solution build
and all-target packaging. Current runtime/pixel checks pass. Actual GPU, UI,
concurrent rendering and native-memory validation require separate workloads.
R-006 remains In progress and work remains uncommitted.

## Local evidence and reproduction

Use the existing [benchmark commands](../../VectorTileRenderer.Benchmarks/README.md).
Matrix uses `--request-timing . artifacts/post-preparation-request-matrix 20 * png-comparison`.
Repeat uses `--request-timing . artifacts/post-preparation-request-repeat 50 bright-z14-8580-10646-512 png-comparison`.
Each trace uses the request event provider and `--request-cpu-workload . artifacts/request-cpu-cache 150`.
Resolve each capture with `--request-cpu-report`.

- [Matrix](../../artifacts/post-preparation-request-matrix/20261116-044149-1340acc1/results.json)
  and [focused repeat](../../artifacts/post-preparation-request-repeat/20261116-044254-306d0ee3/results.json).
- [Disjoint stages CSV](../../artifacts/allocation-trace/post-preparation-request-stages.csv).
- [First trace report](../../artifacts/allocation-trace/post-preparation-request.json)
  and [repeat](../../artifacts/allocation-trace/post-preparation-request-repeat.json).
- [Inclusive method shares](../../artifacts/allocation-trace/post-preparation-hot-methods.csv)
  and [deduplicated stack unions](../../artifacts/allocation-trace/post-preparation-stack-unions.csv).
- [Eight reference hash checks](../../artifacts/allocation-trace/post-preparation-pixel-equality.json).
- Raw traces in `artifacts/allocation-trace/post-preparation-request.nettrace`
  and `post-preparation-request-repeat.nettrace`.
