# Complete request profile after style evaluation changes — 2026-11-15

Inspected `f79f882` plus pending [value/filter changes](Style-Evaluation-2026-11-15.md)
and preceding performance work. Preparation/style still leads combined stages.
Fresh decode is close, and drawing remains substantial. Repeated recursive filter
and token evaluation remains the strongest shared preparation lead.

## Workload and validation

Windows 11 Pro 26200, i7-13700K, 24 logical processors, approximately 31.3 GiB RAM,
SDK 10.0.400, .NET 10.0.11 x64, Skia CPU backend. The harness reports Skia assembly
3.119.0.0. Checked-in Zurich MBTiles and Basic/Bright styles with repository fonts.
Native TMS z12/2145/2662 and z14/8580/10646, 256/512 pixels. OS filesystem caches
are warmed/uncontrolled. UTC artifact dates fall on November 16, local November 15.

Eight cases × six rotating modes × 20 samples, followed by 50 samples/mode on heavy
Bright z14 at 512px. Five warmups/mode. All 1,260 timed outputs passed exact pixels
and publication checks. All eight reference PNG hashes match the preceding style
checkpoint. Timings include request setup/disposal and optional client encoding,
with render/cache observers enabled. Pixel validation occurs outside timers and
can affect later GC.

Two separate traces each contain 150 fresh misses, 150 reused-provider misses and
150 cache hits. Observers are disabled inside marked windows. Final outputs and
all 622 published PNGs pass exact CPU pixel checks. Both traces report zero lost
events. Fresh/warm/hit sample counts are 4,615/3,506/188 and 4,472/3,531/185.
All timings, traces and builds ran sequentially. No forced GC.

## Current complete-request costs

Heavy direct-PNG matrix/repeat medians are 96.26/84.95 ms fresh, 64.85/62.43 ms
reused-provider miss, and 2.43/2.38 ms hit. Repeat p95 is 109.77 ms fresh,
76.41 ms warm and 3.19 ms hit. These captures prioritize current stages. Host/GC
variation prevents treating differences from older profiles as a paired speedup.

Disjoint stage durations below are per-sample means from the focused repeat.
Other preparation is BuildMs minus FetchMs minus StyleMs. Mean shares add to the
external total. Profiling observers add per-feature timing/counter overhead.

| Stage | Fresh mean ms | Share | Reused-provider mean ms | Share |
|---|---:|---:|---:|---:|
| Preparation and style | 29.77 | 33.9% | 29.70 | 47.8% |
| Fetch/decode | 27.08 | 30.8% | <0.01 | <0.1% |
| Geometry and text drawing | 23.01 | 26.2% | 24.93 | 40.1% |
| PNG encoding/publication | 7.13 | 8.1% | 7.16 | 11.5% |
| Setup, lookup, finish and remainder | 0.81 | 0.9% | 0.35 | 0.6% |
| Total | 87.81 | 100% | 62.14 | 100% |

Preparation comprises timed style/filter evaluation 12.97 ms fresh / 13.25 ms warm,
and other preparation 16.80/16.45 ms. Drawing comprises geometry 15.45/17.37 ms
and text 7.56/7.56 ms. The other-preparation timer includes attribute/geometry
preparation, layer construction and profiling overhead, so its full duration
cannot be assigned to a particular operation from these counters alone.

Cache hits average 2.50 ms, 99.5% in lookup/full-image validation.
Trace-window managed allocation is 29.78 MiB fresh, 11.74 MiB warm and 0.32 MiB
hit per request. Native and retained memory are outside these counters.

## Repeated stack evidence

Inclusive shares overlap, with recursive occurrences counted once per sample.
These are sampled target-thread wall-time observations. Native internals,
inlining and runtime polling constrain attribution.

| Stack entry | Fresh share | Warm share |
|---|---:|---:|
| Style.GetValue | 18.6–19.5% | 23.8–25.6% |
| Style.ValidateUsingFilter | 17.5–17.7% | 23.6% |
| Style.ParseStyle | 4.2–4.8% | 3.2–4.8% |
| PbfParser.ParseRendererTile | 16.7% | |
| SkiaCanvas.DrawLineString | 16.8–17.6% | 21.6% |
| SkiaCanvas.DrawPolygon | 11.5% | 12.9–13.2% |
| SkiaCanvas.GetPathFromGeometry | 7.2% | 7.8–7.9% |
| Combined text/path-text drawing | 11.6–11.7% | 14.7–14.9% |
| Renderer.EncodeCachePng | 10.0–10.1% | 13.1–13.4% |
| Renderer.PublishPng | 2.3% | 2.7–2.9% |
| Renderer.LocalizeGeometry | 0.5–0.6% | 0.7–1.4% |

GC-poll frames occur in 24.9–25.5% of fresh and 25.2–27.1% of warm samples.
These counts do not measure GC-worker CPU or pause duration. Draw entry frames
include native rasterization. Cache-hit codec decoding remains prominent.

## Source interpretation and next slice

[Renderer.Render](../../VectorTileRenderer/Renderer.cs) already caches copied
attributes and localized geometry per feature within a request. Source inspection
confirms [Style.NeedsFeatureAttributes](../../VectorTileRenderer/Style.cs) treats
every nonempty filter as feature-dependent, alongside paint/layout dependencies.
Accepted feature/layer pairs then call ValidateLayer and ParseStyle.

The recent change lowers allocation and individual evaluation costs. Repeated
recursive evaluation remains prominent. Next isolate filter-tree interpretation,
literal token resolution and repeated per-feature/layer work before proposing a
request-local prepared evaluator. Preserve typed membership, current-culture
comparisons, missing attributes, short-circuit/exception order and mutable inputs.
Measure other preparation separately if attributing its cost to attribute copies
or layer construction. ParseStyle alone has a smaller sampled share than filtering.

Fresh decoding and native drawing remain parallel leads. PNG has a smaller timed
share. Concurrency, UI scheduling and actual GPU rendering require their own scope.

GitNexus reports the committed index current at HEAD. Concept query returns no
flows with `FTS indexes missing — keyword search degraded. Run: gitnexus analyze
--force to rebuild indexes.` Exact context for NeedsFeatureAttributes identifies
Renderer.Render and TokenUsesFeatureAttributes. Direct source confirms those
relationships and the pending implementation. The index omits dirty additions.

Measurements/documentation only in this slice. Benchmark build passes. The preceding
implementation checkpoint passed 186 Release tests, restore-enabled solution build
and all-target packaging. Current runtime/pixel checks pass. R-006 stays In progress.

## Reproduction and evidence

Use the existing [benchmark commands](../../VectorTileRenderer.Benchmarks/README.md).
Matrix uses `--request-timing . artifacts/post-style-request-matrix 20 * png-comparison`.
Focused repeat uses `--request-timing . artifacts/post-style-request-repeat 50 bright-z14-8580-10646-512 png-comparison`.
Each trace uses the request event provider and `--request-cpu-workload . artifacts/request-cpu-cache 150`.
Resolve each using `--request-cpu-report` after capture.

- [Matrix](../../artifacts/post-style-request-matrix/20261116-042049-ba259983/results.json)
  and [repeat](../../artifacts/post-style-request-repeat/20261116-042145-56a851b3/results.json).
- [Stage means](../../artifacts/allocation-trace/post-style-request-stages.csv)
  and [inclusive method shares](../../artifacts/allocation-trace/post-style-hot-methods.csv).
- [First trace report](../../artifacts/allocation-trace/post-style-request.json)
  and [repeat](../../artifacts/allocation-trace/post-style-request-repeat.json).
- [Eight reference hash checks](../../artifacts/allocation-trace/post-style-pixel-equality.json).
- Raw traces `artifacts/allocation-trace/post-style-request.nettrace` and
  `post-style-request-repeat.nettrace`. Work remains uncommitted.
