# Complete request profile after PNG optimization — 2026-11-15

Inspected `f79f882` plus pending [PNG encoding](Png-Encoding-2026-11-15.md), direct
conversion and earlier performance work. **Combined geometry/text drawing now
leads the heavy request's mean stage budget.** Preparation/style work is close,
and fresh fetch/decode remains substantial. Geometry/path work is the next bounded
investigation target.

## Workloads and limits

Same Windows 11 Pro 26200 / i7-13700K / 24 logical processors / 31.3 GiB host,
SDK 10.0.400, .NET 10.0.11 x64, SkiaSharp 3.119.2. CPU rendering, warmed uncontrolled
OS file caches, sequential requests. The existing timing harness ran eight Zurich
cases (z12/z14, Basic/Bright, 256/512) × six modes × 20 measured samples, followed
by 50 samples/mode for Bright z14/8580/10646 at 512. Five warmups per mode.
All 1,260 measured requests pass exact pixel checks and publication verification.

Two separate stack captures each ran 150 fresh misses, 150 reused-provider misses
and 150 hits. Renderer observers are enabled for unprofiled stage timings and
disabled in stack windows. Every traced publication is checked after the windows,
622 files total. Both traces have zero lost events. Fresh/warm/hit sample counts
are 5,761 / 3,821 / 230 and 5,126 / 3,613 / 194.

The matrix showed considerable host-time variation. Bright z12 512 fresh direct-PNG
median was 212.79 ms, while the heavy z14 512 median was 113.45 ms. A read-only host
snapshot showed other active processes but did not establish the cause. The focused
repeat was closer to the preceding PNG checkpoint, fresh median 99.40 ms, warm
67.81 ms and hit 2.56 ms. Fresh/warm p95 was 144.45 / 100.99 ms. This run supports
stage prioritization; it supplies no new before/after latency-regression claim.

## Heavy focused-repeat breakdown

Shares use **means** of per-sample disjoint durations. Other preparation is
`BuildMs - FetchMs - StyleMs`; setup, lookup, finish/readback and remaining timing
gaps complete the external total. Median request times above are separate summaries.

| Stage | Fresh mean | Share | Reused-provider miss mean | Share |
| --- | --- | --- | --- | --- |
| Geometry drawing | 27.16 ms | 26.3% | 26.22 ms | 36.4% |
| Text drawing | 7.85 ms | 7.6% | 7.88 ms | 10.9% |
| Timed style/filter evaluation | 13.23 ms | 12.8% | 13.41 ms | 18.6% |
| Other preparation | 16.39 ms | 15.9% | 16.04 ms | 22.2% |
| Fetch/decode | 29.56 ms | 28.7% | <0.01 ms | <0.1% |
| PNG encoding/publication | 7.89 ms | 7.6% | 8.09 ms | 11.2% |
| Setup, lookup, finish and remainder | 1.09 ms | 1.1% | 0.49 ms | 0.7% |
| Total mean | 103.17 ms | 100% | 72.13 ms | 100% |

Combined drawing is 35.01 ms / 33.9% fresh and 34.10 ms / 47.3% warm.
Preparation plus style is 29.62 ms / 28.7% fresh and 29.45 ms / 40.8% warm.
Fresh fetch/decode is the largest individual timer by a small margin; drawing is
the largest combined stage and benefits requests with both fresh and reused tiles.
PNG now occupies about 8–11% of these means. Hits average 2.69 ms, with 99.6% in
cache lookup/validation.

## Stack evidence

The following inclusive managed-entry shares repeat across both captures. Rows
overlap, including path-construction descendants and filter/value descendants.

| Stack entry | Fresh sample share | Warm sample share |
| --- | --- | --- |
| `SkiaCanvas.DrawLineString` | 17.1–17.4% | 22.8–23.1% |
| `SkiaCanvas.DrawPolygon` | 11.1–11.6% | 14.7–15.3% |
| `SkiaCanvas.GetPathFromGeometry` | 7.8% | 9.4–9.6% |
| Combined text/path-text drawing | 10.6–10.8% | 14.2% |
| `Style.GetValue` | 18.1–18.7% | 23.3–23.5% |
| `Style.ValidateUsingFilter` | 16.6–17.3% | 22.4–22.9% |
| `PbfParser.ParseRendererTile` | 17.1–17.6% | |
| `Renderer.EncodeCachePng` | 8.9–9.2% | 12.3–12.5% |
| `Renderer.PublishPng` | 2.3–2.7% | 3.0–3.3% |

The second capture's union of drawing stacks occupies 39.4% fresh and 52.4% warm.
Encoder frames sometimes inline into `EncodeCachePng`; matching only
`SKPixmap.Encode` misses most warm encoder samples. Hit `SKCodec.GetPixels` occurs
in 77.0–84.5% of samples, consistent with full decode validation.

Source inspection confirms
[GetPathFromGeometry](../../VectorTileRenderer/SkiaCanvas.cs) creates a new SKPath,
MoveTo/LineTo commands and an iterator for `geometry.Skip(1)` per call. Drawing
invokes it repeatedly. [ValidateUsingFilter/GetValue](../../VectorTileRenderer/Style.cs)
perform recursive filter evaluation and value resolution. The zoom-stop branch
also constructs tuple/list data per evaluation. These source observations identify
candidate work; the trace does not isolate the benefit of any proposed change.

## Interpretation and next experiment

Investigate line/polygon drawing and path construction first, separating repeated
path construction from native DrawPath cost. A per-request path reuse or simpler
construction experiment must preserve clipping, path mutation rules, native-resource
disposal and exact CPU pixels. Measure complete requests before accepting it.
Preparation/filter evaluation is a comparable follow-up target; source and stacks
support a separate bounded expression/filter experiment.

Samples describe marked-thread wall time. Native codec/rasterizer internals and
JIT inlining limit attribution. GC-poll frames occupy 22–24% of miss samples;
GC-worker CPU and pause duration require separate evidence. Observer overhead
also differs between timed and traced workloads. Traces report about 32.44 MiB
fresh, 14.40 MiB warm and 0.32 MiB hit managed bytes/request. Native/retained memory,
GPU performance, concurrency and interactive UI scheduling are outside scope.

GitNexus matches committed HEAD. Concept query returned no flows with
`FTS indexes missing — keyword search degraded`. Exact context for
`Style.ValidateUsingFilter` identifies `ValidateLayer`, `FindLayers`, recursion and
`GetValue`; source confirms the call path. Pending-source symbols and native Skia
internals require direct source/runtime inspection. This slice adds measurements
and documentation. The preceding implementation checkpoint passed 158 tests/full
build/pack; no renderer edit was made during this profile.

## Local evidence

Reproduction follows the existing commands in the
[benchmark README](../../VectorTileRenderer.Benchmarks/README.md), using the new
output names below. Timing and stack capture ran sequentially.

- [Eight-case timing matrix](../../artifacts/post-png-request-matrix/20261115-223604-1bd7b4ae/results.json)
  and [focused repeat](../../artifacts/post-png-request-repeat/20261115-223743-44bcf141/results.json).
- [Disjoint stage means/shares CSV](../../artifacts/allocation-trace/post-png-request-stages.csv).
- [Resolved stack capture](../../artifacts/allocation-trace/post-png-request.json)
  and [repeat](../../artifacts/allocation-trace/post-png-request-repeat.json).
- [Inclusive hot-method CSV](../../artifacts/allocation-trace/post-png-hot-methods.csv).
- Raw traces in `artifacts/allocation-trace/post-png-request.nettrace` and
  `post-png-request-repeat.nettrace`.

Work remains uncommitted; R-006 remains In progress.
