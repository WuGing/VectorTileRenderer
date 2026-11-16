# Complete request profile after drawing changes — 2026-11-15

Inspected `f79f882` plus pending [drawing/path changes](Path-Drawing-2026-11-15.md)
and earlier performance work. **Preparation/style work now leads the focused
repeat's combined timed stages.** Fresh fetch/decode and combined drawing remain
close. Value/filter evaluation is the strongest next common investigation target.

## Workloads

Same Windows 11 Pro 26200 / i7-13700K / 24 logical processors / 31.3 GiB host,
SDK 10.0.400, .NET 10.0.11 x64, SkiaSharp 3.119.2. CPU, sequential requests,
warmed/uncontrolled OS file caches. Timing and tracing ran separately, with no
parallel test/build workload. UTC artifact timestamps fall on November 16; local
date remains November 15.

The existing request harness ran eight cases (Zurich z12/z14, Basic/Bright, 256/512),
six rotating modes, 20 measured samples/mode. Heavy Bright z14/8580/10646 at 512
then ran 50 samples/mode. Five warmups per mode. All 1,260 measured requests passed
exact output pixels and publication checks. All eight render reference PNGs match
the preceding drawing checkpoint byte for byte.

Two independent trace captures each ran 150 fresh misses, 150 reused-provider misses
and 150 hits. Renderer observers were disabled in the marked windows, and final
outputs/all publications were checked afterward. Both captures have zero lost
events. Fresh/warm/hit sample counts are 4,476 / 3,586 / 198 and 4,713 / 3,602 / 216.
All 622 published PNGs matched their CPU references.

## Current latency and disjoint stage means

Heavy direct-PNG matrix/repeat medians are 99.67 / 94.78 ms fresh, 69.88 / 69.63 ms
reused-provider miss and 2.96 / 2.76 ms hit. Repeat p95 is 209.42 ms fresh and
132.72 ms warm. Host/GC timing variability remains substantial, so this profile
serves stage prioritization. It supplies no additional paired speedup measurement.

Stage shares below are per-sample disjoint durations aggregated as **means**.
Other preparation is `BuildMs - FetchMs - StyleMs`. Setup, lookup, finish/readback
and remaining timing gaps complete the external total. Timed observers add
per-feature measurement/counter overhead.

| Heavy focused-repeat stage | Fresh mean | Share | Reused-provider miss mean | Share |
| --- | --- | --- | --- | --- |
| Timed style/filter evaluation | 16.84 ms | 15.9% | 20.23 ms | 25.0% |
| Other preparation | 17.25 ms | 16.3% | 17.36 ms | 21.5% |
| Fetch/decode | 31.03 ms | 29.3% | <0.01 ms | <0.1% |
| Geometry drawing | 19.97 ms | 18.9% | 22.22 ms | 27.5% |
| Text drawing | 10.74 ms | 10.2% | 10.71 ms | 13.3% |
| PNG encoding/publication | 8.61 ms | 8.1% | 9.72 ms | 12.0% |
| Setup, lookup, finish and remainder | 1.37 ms | 1.3% | 0.53 ms | 0.7% |
| Total mean | 105.81 ms | 100% | 80.79 ms | 100% |

Preparation/style combines to **34.09 ms / 32.2% fresh** and **37.60 ms / 46.5%
warm**. Drawing combines to 30.71 ms / 29.0% fresh and 32.94 ms / 40.8% warm.
Fresh decode is 31.03 ms / 29.3%, the largest individual timer. Thus fresh requests
have three closely sized costs; preparation leads by a clearer margin when the
decoded model is reused. Hits average 2.95 ms, 99.5% in lookup/validation.

## Repeated stack evidence

These inclusive managed-entry shares overlap. Recursive occurrences of one method
count once per sample. They describe marked-thread wall time, with native entry
frames, JIT inlining and runtime polling effects.

| Stack entry | Fresh share | Warm share |
| --- | --- | --- |
| `Style.GetValue` | 20.1–20.6% | 24.3–25.5% |
| `Style.ValidateUsingFilter` | 17.9–18.7% | 22.3–23.1% |
| `Style.ParseStyle` | 6.0–6.2% | 5.3–5.7% |
| `PbfParser.ParseRendererTile` | 13.8–13.9% | |
| `SkiaCanvas.DrawLineString` | 16.5–16.7% | 21.2–21.4% |
| `SkiaCanvas.DrawPolygon` | 11.1–11.5% | 12.7–12.8% |
| `SkiaCanvas.GetPathFromGeometry` | 7.2–7.5% | 7.4–8.1% |
| Combined text/path-text drawing | 11.6–11.9% | 14.9–15.4% |
| `Renderer.EncodeCachePng` | 9.8–10.0% | 12.8% |
| `Renderer.PublishPng` | 2.5% | 3.4% |

Value resolution and recursive filtering are prominent in both traces. DrawLineString
and DrawPolygon include native rasterization and child path work. Paint helpers
usually inline, so their named frames give limited separate attribution. Hit
`SKCodec.GetPixels` appears in 78.2–85.4% of samples, consistent with full validation.

GC-poll frames appear in 23.8–25.0% of fresh and 25.2–26.5% of warm samples.
GC-worker CPU, pause duration and native rasterizer internals require separate
evidence. Traces report 30.47 MiB fresh, 12.43 MiB warm and 0.32 MiB hit managed
allocation/request. Native/retained memory, concurrency, UI scheduling and actual
GPU rendering are outside this experiment.

## Source inspection and next target

[Style.ValidateUsingFilter/GetValue](../../VectorTileRenderer/Style.cs) evaluate
object-array filters recursively, resolve tokens and perform numeric/type
comparisons. The zoom-stop branch constructs tuple/list data on each evaluation.
[Renderer](../../VectorTileRenderer/Renderer.cs) also prepares feature attributes,
localized geometry and visual layers. These source observations identify candidate
work; the trace does not establish which GetValue branch supplies the most benefit.

Next inspect and isolate **filter/value evaluation and layer preparation**, with
particular attention to repeated work that can be resolved per layer/request.
Preserve type/comparison/missing-attribute semantics, zoom behavior and mutable style
inputs. Require exact pixels and complete-request comparison for any candidate.
Drawing and fresh decoding remain substantial parallel leads.

GitNexus committed index matches HEAD. Concept query returns no flows with
`FTS indexes missing — keyword search degraded`. Exact context for
`Style.ValidateUsingFilter` identifies ValidateLayer, FindLayers, recursion,
GetValue and IsNumber. Source inspection confirms the flow; pending source and
native internals require direct/runtime inspection.

This slice changes measurements/docs only. The preceding implementation checkpoint
passed 173 Release tests, 15 Debug path tests, full build and pack. Current pixel
checks and traced workload validations completed successfully.

## Local evidence

Reproduction uses the existing commands in the
[benchmark README](../../VectorTileRenderer.Benchmarks/README.md) with the output
paths below. Work remains uncommitted; R-006 remains In progress.

- [Eight-case matrix](../../artifacts/post-path-request-matrix/20261116-035315-a9992ba8/results.json)
  and [focused repeat](../../artifacts/post-path-request-repeat/20261116-035421-eea70761/results.json).
- [Disjoint stage means/shares CSV](../../artifacts/allocation-trace/post-path-request-stages.csv).
- [Resolved first trace](../../artifacts/allocation-trace/post-path-request.json)
  and [repeat](../../artifacts/allocation-trace/post-path-request-repeat.json).
- [Inclusive hot-method samples CSV](../../artifacts/allocation-trace/post-path-hot-methods.csv).
- Raw traces in `artifacts/allocation-trace/post-path-request.nettrace` and
  `post-path-request-repeat.nettrace`.
