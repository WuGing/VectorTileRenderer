# Complete request profile and cumulative progress — 2026-11-15

Inspected `f79f882` plus pending work through
[scratch-path submission](Path-Submission-2026-11-15.md). Fresh decoding is now
the largest individual instrumented stage, 34%. Observer-free drawing remains
the largest sampled group, approximately 38% fresh / 50% reused-provider.
Filtering remains substantial. Path construction now occupies about 3–4% of samples.

Direct cumulative comparisons against saved builds show meaningful overall gains.
From the saved pre-parser build, heavy fresh misses are 42–43% faster and reused
tile misses 44–50% faster, with 40%/46% less managed allocation respectively.
That starting build already includes direct PNG output. The recent three slices
together measure 8–12% faster fresh / 19–26% reused-provider requests.

## Workload and validation

Windows 11 Pro 26200, i7-13700K, 24 logical processors, approximately 31.3 GiB RAM,
SDK 10.0.400, .NET 10.0.11 x64, Skia assembly 3.119.0.0 CPU. Checked-in Zurich
MBTiles, Basic/Bright styles and repository fonts. Native TMS z12/2145/2662 and
z14/8580/10646, 256/512px. OS filesystem caches are warmed/uncontrolled. UTC
artifact timestamps are November 16, local date November 15. No forced GC.
Builds, timing runs and captures execute sequentially.

Eight cases × six rotating modes × 20 samples, then 50 samples/mode for heavy
Bright z14/512px. Five warmups/mode. All 1,260 measured outputs and cache publications
pass exact CPU pixels. All eight reference PNG hashes match the prior checkpoint.
Render/cache observers are enabled for stage timing. Validation runs outside timers
and may influence subsequent GC.

Two independent traces each contain 150 fresh misses, 150 reused-provider misses
and 150 hits, with observers disabled inside marked windows. Final outputs and
all 622 published PNGs match. Both traces have zero lost events. Fresh/warm/hit
sample counts are 3,792/2,808/166 and 3,922/2,498/180.

Four alternating same-process cumulative comparisons add 1,440 exact-pixel requests,
for 2,700 timed requests in this slice. Each paired run rotates fresh/warm/hit modes,
alternates implementation order each round, and has 60 samples/mode/implementation
plus five warmups. Each implementation owns its style/provider/cache. Observers
are disabled. Timings include reflected invocation, setup, request, encoding/
publication and disposal. All outputs and publications are decoded and compared
outside timers. Saved baseline library hashes identify each comparison.

## How much has accumulated

The totals below are direct cumulative comparisons. Earlier per-slice percentages
were measured in different runs. Heavy Bright z14/8580/10646, 512px, direct PNG.

### Parser-through-current sequence

The saved `artifacts/attribute-before-bin` build predates the attribute parser,
fused conversion, PNG encoder, path/paint, style/filter/preparation and scratch-path
changes. It already contains direct PNG output and the earlier style-allocation
work. Thus this comparison covers the subsequent CPU optimization sequence.

| Run/mode | Saved baseline median ms | Current median ms | Lower elapsed | Baseline p95 ms | Current p95 ms |
|---|---:|---:|---:|---:|---:|
| First fresh miss | 120.88 | 70.17 | 42.0% | 136.96 | 85.71 |
| Repeat fresh miss | 121.82 | 69.64 | 42.8% | 138.54 | 82.24 |
| First reused-provider miss | 81.58 | 40.89 | 49.9% | 92.51 | 54.36 |
| Repeat reused-provider miss | 79.50 | 44.76 | 43.7% | 92.75 | 56.55 |

Baseline/current median ratios are approximately 1.7–1.8× fresh and 1.8–2.0× for
reused-provider misses in this sequential workload. Managed allocation falls
44.80 → 27.05 MB fresh (39.6%) and 15.16 → 8.13 MB warm (46.4%). Cache-hit medians
are 2.89 → 2.36 ms and 2.87 → 2.34 ms, alongside smaller optimized PNG output.

The earlier [direct-PNG experiment](Direct-Png-2026-11-15.md) separately measured
the bitmap-plus-client-encode migration, including heavy hits 31.30 → 2.80 ms.
That result has its own baseline and scope. GUI frame rate, concurrent throughput
and actual GPU performance require their own measurements.

### Recent three slices together

`artifacts/style-eval-before-bin` predates the value/filter, layer-preparation and
scratch-path changes, while already including the earlier parser/PNG/path work.

| Run/mode | Saved baseline median ms | Current median ms | Lower elapsed |
|---|---:|---:|---:|
| First fresh miss | 89.39 | 78.43 | 12.3% |
| Repeat fresh miss | 100.25 | 92.19 | 8.0% |
| First reused-provider miss | 57.94 | 47.08 | 18.7% |
| Repeat reused-provider miss | 67.64 | 50.30 | 25.6% |

Recent warm managed allocation falls 13.03 → 8.13 MB (37.6%), fresh 31.95 →
27.05 MB (15.3%). The recent repeat has substantial tails and host variation,
including hit p95 4.40 → 11.65 ms. Alternation reduces drift while retaining GC,
validation and JIT interactions. The broad cumulative comparison is more stable.

Saved baseline hashes are

- Pre-parser `1DE5B765B9A21FCBF03EB506A6C74B5FA9E32B3F96C410961EC1CA57BA277C98`.
- Pre-style `9FC4989A08251129476637C4BD05A3D89B97DCBFC992DFC561C6BB5B909010E2`.
- Current `B942706ABA6FF1B32485AAA784187220FBBE9C1024A22EF1F734FE46911B436B`.

## Current instrumented stages

Heavy direct-PNG matrix/repeat medians are 75.43/72.34 ms fresh, 44.53/50.18 ms
reused-provider miss and 2.30/2.32 ms hit. Focused repeat p95 is 88.75 ms fresh,
58.16 ms warm and 2.75 ms hit. These serve current-stage attribution.

Disjoint stage durations below are per-sample means from the focused repeat.
Other preparation is BuildMs minus FetchMs minus StyleMs. Shares add to external
total. Per-feature observers add work during preparation and may affect ranking.

| Stage | Fresh mean ms | Share | Reused-provider mean ms | Share |
|---|---:|---:|---:|---:|
| Fetch/decode | 24.93 | 34.0% | <0.01 | <0.1% |
| Preparation and style | 21.92 | 29.9% | 23.88 | 48.2% |
| Geometry and text drawing | 18.79 | 25.6% | 18.58 | 37.5% |
| PNG encoding/publication | 6.74 | 9.2% | 6.84 | 13.8% |
| Setup, lookup, finish and remainder | 0.89 | 1.2% | 0.28 | 0.6% |
| Total | 73.27 | 100% | 49.58 | 100% |

Preparation comprises timed style/filter evaluation 8.32 ms fresh / 8.78 ms warm
and other preparation 13.60/15.09 ms. Drawing comprises geometry 12.30/12.04 ms
and text 6.49/6.54 ms. Other preparation includes counters, layer construction,
attribute/geometry work and ordering, requiring finer attribution for a specific
operation claim. Hits average 2.37 ms, 99.6% lookup/full-image validation.

Trace-window managed allocation is 25.80 MiB fresh, 7.76 MiB warm and 0.32 MiB hit.
Native and retained memory are excluded.

## Observer-free samples

These are sampled target-thread wall-time shares, with native-entry, runtime-polling
and inlining limitations. Inclusive entries overlap. Each union counts a matching
stack once, and recursive occurrences count once per method/sample.

| Entry/group | Fresh share | Warm share |
|---|---:|---:|
| Drawing-entry union | 37.5–37.8% | 49.8–50.1% |
| Filter/value/style-entry union | 17.6–18.2% | 26.1–26.9% |
| PbfParser.ParseRendererTile | 23.5–24.1% | |
| Style.ValidateUsingFilter | 16.8–17.5% | 25.1–25.6% |
| Style.GetValue | 15.1–16.3% | 22.7–23.4% |
| Style.ParseStyle | 0.7–0.9% | 1.0–1.3% |
| SkiaCanvas.DrawLineString | 16.4–16.7% | 22.3–23.1% |
| SkiaCanvas.DrawPolygon | 7.9–8.5% | 10.5% |
| SkiaCanvas.SetPathGeometry | 2.9–3.0% | 3.2–4.1% |
| Combined text/path-text drawing | 12.9% | 16.2–17.3% |
| Renderer.EncodeCachePng | 11.0–11.6% | 14.2–15.8% |
| Renderer.PublishPng | 2.0% | 2.4–2.6% |

Drawing union covers line/polygon/point/text entry frames. Style union covers
GetValue, ValidateUsingFilter and ParseStyle. The fresh parser entry includes
protobuf/model/geometry work. The first capture contains 582 inclusive samples in
protobuf ReadRepeated-related frames, about 15% of fresh samples, inside the parser
entry. These child shares overlap. GC-poll frames occur in ~24–25% of samples,
providing no exact GC-worker CPU or pause-duration budget.

## Interpretation and next target

For new tiles, fetch/decode is the largest individual timed stage and parsing is
the largest named managed entry. For reused tiles, observer-free drawing is the
largest group, with filtering also substantial. Instrumented preparation remains
large and contains observer overhead. The profile supports workload-specific
priorities across these remaining costs.

Path construction now has a much smaller sampled share. Next isolate fresh decode's
protobuf/model/geometry costs if optimizing panning into new tiles. For repeated
decoded-tile rendering, native drawing and filter evaluation remain the main leads.
Any further candidate requires complete-request comparison and exact pixels.

Source inspection confirms the shared [parser](../../VectorTileRenderer/Sources/PbfParser.cs),
[filter evaluator](../../VectorTileRenderer/Style.cs) and
[drawing path](../../VectorTileRenderer/SkiaCanvas.cs). GitNexus reports its committed
index current at HEAD. Concept query returns no flows with `FTS indexes missing —
keyword search degraded. Run: gitnexus analyze --force to rebuild indexes.` Exact
ValidateUsingFilter context identifies ValidateLayer, FindLayers and recursive calls.
Direct source verifies the pending switch implementation and scratch-path lifetime.
Dirty additions are outside the indexed snapshot.

Measurements/docs only in this slice. Benchmark build and runtime pixel checks pass.
The preceding implementation checkpoint passed 196 Release tests, 21 Debug path
tests, restore-enabled solution build and all-target packaging. Work is uncommitted
and R-006 remains In progress.

## Reproduction and local evidence

Use the existing [benchmark commands](../../VectorTileRenderer.Benchmarks/README.md).
Matrix uses `--request-timing . artifacts/post-submission-request-matrix 20 * png-comparison`.
Repeat uses `--request-timing . artifacts/post-submission-request-repeat 50 bright-z14-8580-10646-512 png-comparison`.
Each trace uses the request provider and `--request-cpu-workload . artifacts/request-cpu-cache 150`.
Resolve with `--request-cpu-report`.

```powershell
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --paired-request-comparison . artifacts/attribute-before-bin/WuGing.VectorTileRenderer.dll artifacts/cumulative-parser-to-current.json 60
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --paired-request-comparison . artifacts/style-eval-before-bin/WuGing.VectorTileRenderer.dll artifacts/cumulative-style-to-current.json 60
```

- [Matrix](../../artifacts/post-submission-request-matrix/20261116-050648-b30af1fa/results.json)
  and [repeat](../../artifacts/post-submission-request-repeat/20261116-050736-4168e82c/results.json).
- [Stage means](../../artifacts/allocation-trace/post-submission-request-stages.csv),
  [hot methods](../../artifacts/allocation-trace/post-submission-hot-methods.csv),
  [stack unions](../../artifacts/allocation-trace/post-submission-stack-unions.csv).
- [First trace](../../artifacts/allocation-trace/post-submission-request.json)
  and [repeat](../../artifacts/allocation-trace/post-submission-request-repeat.json).
- [Eight reference checks](../../artifacts/allocation-trace/post-submission-pixel-equality.json).
- [Pre-parser comparison](../../artifacts/cumulative-parser-to-current.json)
  and [repeat](../../artifacts/cumulative-parser-to-current-repeat.json).
- [Pre-style comparison](../../artifacts/cumulative-style-to-current.json)
  and [repeat](../../artifacts/cumulative-style-to-current-repeat.json).
- [Cumulative summary CSV](../../artifacts/cumulative-progress-summary.csv).
- Raw traces `artifacts/allocation-trace/post-submission-request.nettrace`
  and `post-submission-request-repeat.nettrace`.
