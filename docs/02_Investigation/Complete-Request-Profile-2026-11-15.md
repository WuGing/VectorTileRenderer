# Complete request profile — 2026-11-15

Inspected `f79f882` plus pending [direct conversion](Fused-Conversion-2026-11-15.md)
and earlier performance changes. PNG encoding, preparation/style evaluation and
drawing are substantial alongside fresh decode. PNG encoding is the clearest
single next experiment for both fresh and reused-provider misses.

## Workloads and collection

Windows 11 Pro 26200, i7-13700K, 24 logical processors, 31.3 GiB visible RAM,
SDK 10.0.400, .NET 10.0.11 x64, SkiaSharp 3.119.2. Checked-in Zurich native z12 and
z14 tiles, Basic/Bright styles, 256/512 output sizes, actual CPU rendering.
Warm OS file caches, one synchronous request at a time.

The existing [timing harness](../../VectorTileRenderer.Benchmarks/RequestTiming.cs)
ran eight cases × six rotating modes × 20 samples (960 requests), followed by a
50-sample/mode repeat for Bright z14/8580/10646 at 512 (300 requests). Five warmups
per mode. Both profiling observers are enabled for stage timings. All requests
check exact output pixels outside timers and verify published PNGs.

Separately, [RequestCpuWorkload](../../VectorTileRenderer.Benchmarks/RequestCpuWorkload.cs)
ran two independent stack captures, each with 150 fresh misses, 150 reused-provider
misses and 150 hits. Renderer observers are disabled in these windows. Fresh
creates/disposes the source, reused-provider uses its preloaded decoded tile, and
hit reads an existing PNG. Misses use unique revision tokens. New CPU canvases,
provider binding, rendering, PNG encoding, publication and disposal execute inside
the windows. Final outputs and all publications are validated after all windows;
generated PNG pruning also happens afterward.

Local dotnet-trace 10.0.750501 uses `dotnet-common,dotnet-sampled-thread-time` plus
the workload's marker provider. The [analyzer](../../VectorTileRenderer.Benchmarks/ParserCpuReport.cs)
selects the marked OS thread, resolves managed stacks and records lost events.
Both captures have **zero lost events**, with fresh/warm/hit sample counts
7,178 / 6,569 / 299 and 8,131 / 6,973 / 303 respectively.

## Current latency and stage breakdown

Heavy Bright 512 direct-PNG medians are 117.98 / 116.16 ms fresh, 96.61 / 92.93 ms
reused-provider miss, and 3.12 / 3.14 ms hit in matrix/repeat runs. Fresh p95 is
128.02 / 163.61 ms, showing host/GC variation. Across the full matrix, fresh medians
range 37.61–117.98 ms, and hit medians range 1.01–3.12 ms.

Stage shares below use **means**, derived from each repeat sample before aggregation.
`BuildVisualLayersMs` includes fetch/decode and timed style work. Other preparation
is `BuildMs - FetchMs - StyleMs`. The remainder is external total less the disjoint
recorded stages, including setup/lookup and optional client encode. It captures
remaining cleanup, ordering and timing gaps.

| Heavy Bright 512 stage | Fresh miss mean | Share | Reused-provider miss mean | Share |
| --- | --- | --- | --- | --- |
| Fetch/decode | 29.04 ms | 23.6% | <0.01 ms | <0.1% |
| Timed style/filter evaluation | 12.65 ms | 10.3% | 13.92 ms | 15.1% |
| Other preparation | 15.99 ms | 13.0% | 20.56 ms | 22.3% |
| Geometry drawing | 23.16 ms | 18.8% | 18.14 ms | 19.6% |
| Text drawing | 9.24 ms | 7.5% | 7.78 ms | 8.4% |
| Finish/readback | 0.01 ms | <0.1% | 0.01 ms | <0.1% |
| PNG encoding/publication | 31.66 ms | 25.7% | 31.50 ms | 34.1% |
| Setup, lookup and remainder | 1.21 ms | 1.0% | 0.48 ms | 0.5% |
| Total mean | 122.96 ms | 100% | 92.40 ms | 100% |

The matrix's heavy fresh mean is 117.29 ms, with PNG 26.5%, fetch/decode 25.5%,
style/other preparation 25.0%, and geometry/text 22.0%. The repeat agrees on the
major costs, with more drawing-time variation. Timed preparation includes layer/
feature traversal, attribute/localized-geometry work, visual-layer construction
and drawing setup. Source regions and observer overhead constrain attribution.

## Sampled hot stacks

These are inclusive target-thread sample shares across two captures. Ancestor/
descendant rows overlap. They estimate thread wall time and include runtime/native
transition effects, JIT inlining and GC polling. They cannot be summed into an
exclusive CPU-time budget.

| Managed stack entry | Fresh sample share | Reused-provider miss share |
| --- | --- | --- |
| `SKPixmap.Encode` | 31.5–32.4% | 37.2–38.0% |
| `Renderer.PublishPng` | 2.0–2.1% | 2.2–2.4% |
| `PbfParser.ParseRendererTile` | 13.9–14.3% | |
| `Style.GetValue` | 13.1–13.6% | 16.1–16.3% |
| `SkiaCanvas.DrawLineString` | 13.0–13.1% | 16.0–16.3% |
| `SkiaCanvas.DrawPolygon` | 8.5–8.6% | 10.8–11.2% |
| `SkiaCanvas.GetPathFromGeometry` | 5.7–6.0% | 7.4% |
| Text/path-text drawing combined | 7.7–8.0% | 9.8–9.9% |
| GC-poll frame | 16.4% | 16.8–17.1% |

Source inspection confirms `GetPathFromGeometry` constructs a new SKPath and adds
points for each draw. Filter stacks pass through `ValidateLayer`,
`ValidateUsingFilter` and `GetValue`. PNG stacks resolve to the managed Skia encoder
entry; internal native codec functions are outside this managed-stack view.
The encoder/publication counts support investigating codec work first.

Hits average 3.25 ms in the repeat, with 99.6% of timed duration in cache lookup/
validation. `SKCodec.GetPixels` appears in 75.2–76.6% of hit samples. This matches
the existing full decode-validation contract. GC-poll frames describe runtime
polling/suspension attribution and do not measure GC-worker CPU or pause duration.
[Collector semantics](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace).

## Interpretation and validation

The strongest next bounded experiment is PNG encoder configuration, checking
complete-request time, encoded size and exact decoded pixels. Preparation/filter
evaluation and SKPath construction are additional measured leads. Fresh decode
still occupies roughly one quarter of instrumented elapsed time on this heavy tile.

Stage timings and traces use different instrumentation. Renderer observers add
per-feature timing/counter work; stack-capture windows disable them. Traces report
32.50 MiB fresh, 14.46 MiB warm and 0.38 MiB hit managed allocation/request. Native
memory, retained heap, interactive Mapsui scheduling, concurrency and GPU timing
are outside scope. Traced elapsed times are excluded from latency claims.

148 tests and full Release solution build pass. All 1,260 unprofiled requests pass
pixel checks. The trace workloads verify nonempty CPU references and all 622
published PNGs after their windows. The change adds benchmark/report tooling.

GitNexus index matches committed HEAD. Query `RenderCachedPng complete tile request`
returns no flows with `FTS indexes missing — keyword search degraded`. Exact-symbol
context for `Renderer.RenderCachedPng#11` finds its core call and demo flows. Source
inspection verifies the actual request stages and pending direct parser path.
Empty graph search supplies no caller assurance.

## Reproduction and local evidence

Commands are in the [benchmark README](../../VectorTileRenderer.Benchmarks/README.md).

- [Timing matrix](../../artifacts/current-request-matrix/20261115-220559-df238b9a/results.json)
  and [heavy repeat](../../artifacts/current-request-repeat/20261115-220708-2dedce8f/results.json).
- [Disjoint stage means/shares CSV](../../artifacts/allocation-trace/complete-request-stages.csv),
  with formulas defined above.
- [First resolved trace](../../artifacts/allocation-trace/complete-request.json) and
  [repeat](../../artifacts/allocation-trace/complete-request-repeat.json).
- [Inclusive hot-method samples CSV](../../artifacts/allocation-trace/complete-request-hot-methods.csv).
- Raw traces in `artifacts/allocation-trace/complete-request.nettrace` and
  `complete-request-repeat.nettrace`.

Next review/checkpoint and define the PNG experiment using this current baseline.
Work remains uncommitted; R-006 remains In progress.
