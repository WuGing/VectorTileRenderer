# Lossless PNG encoding — 2026-11-15

Inspected `f79f882` plus pending direct conversion and performance work. Follow-up
to the [complete request profile](Complete-Request-Profile-2026-11-15.md).

## Selected policy

[Renderer.EncodeCachePng](../../VectorTileRenderer/Renderer.cs) is shared by the
bitmap and byte-array cache miss paths. Vector-only styles use
`SKPngEncoderFilterFlags.None` and zlib level 1. Styles containing any raster source
use `SKPngEncoderOptions.Default`, which the installed SkiaSharp reports as all
filters and level 6. This selection is per request and covers mixed raster/vector
styles. PeekPixels borrows bitmap storage synchronously; encoded SKData has its own
lifetime. Bitmap/data ownership, atomic publication and error handling follow the
existing cache paths.

PNG remains lossless. The options control compression/filtering; the
[official API](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skpngencoderoptions)
exposes row-filter flags and levels 0–9. Cached hits return their existing validated
file bytes. A regression writes a legacy PNG entry and verifies its original bytes
are returned successfully. Both cache APIs share their existing keys and entries.

The raster policy follows measured content differences. On a synthetic translucent
gradient, unfiltered level 1 produces 275,380 bytes versus 39,055 bytes with default
encoding (7.05×). An alpha checker produces 14,608 versus 2,745 bytes (5.32×).
Seeded opaque noise produces 899,221 versus 917,040 bytes. These inputs are authored
at runtime. The fast setting's size benefit applies to the checked map cases;
content determines compression ratio. Raster-containing styles use compact full
filtering in response to the gradient result. A mixed-style regression compares
the resulting bytes with the legacy encoder on a gradient.

## Experiments

Same Windows 11 Pro 26200 / i7-13700K / 24 logical processors / 31.3 GiB host,
SDK 10.0.400, .NET 10.0.11 x64, SkiaSharp 3.119.2. CPU bitmaps from Zurich z12 and
z14, Basic/Bright, 256/512. No profiler or parallel build/test during timings.

[PngEncodingComparison](../../VectorTileRenderer.Benchmarks/PngEncodingComparison.cs)
compares nine modes on fixed rendered bitmaps, rotating order each round, five
warmups and 30 measured encodes/mode. Modes are legacy SKImage PNG encoding,
all filters at levels 0/1/3/6, Sub+Up at 1/3, Up at 1, and None at 1. Main run uses
eight map cases, repeat adds gradient/noise/alpha-checker inputs. This gives 5,130
measured encodes. Every result is decoded and checked against legacy decoded pixels
outside timers. Native result creation is timed; result disposal/validation and
native allocation totals are outside that encoding measurement.

## Encoder results

Across all eight map cases, selected encoding medians are 73–81% lower and files
14–20% smaller in both runs. Explicit all-filter level 6 reproduces the legacy
output size and encoding cost. Heavy Bright 512 repeat results follow.

| Mode | Median encode | PNG bytes |
| --- | --- | --- |
| Legacy/default | 29.55 ms | 389,245 |
| All filters, level 1 | 11.86 ms | 423,449 |
| All filters, level 3 | 15.33 ms | 400,836 |
| Sub+Up, level 1 | 9.04 ms | 406,857 |
| Sub+Up, level 3 | 11.58 ms | 385,304 |
| Up, level 1 | 7.56 ms | 408,918 |
| None, level 1 — selected for vector styles | 5.84 ms | 331,866 |
| All filters, level 0 | 4.93 ms | 1,050,868 |

Selected repeat encoder median is 80.2% lower, with output 14.7% smaller.
Main-run median is 38.53 → 7.48 ms (80.6% lower), demonstrating absolute host-time
variation while the ratio repeats.

## Complete requests

The existing rotating six-mode CPU Bright z14/8580/10646 512 harness ran 50
samples/mode before/after, then 30/mode with process order reversed. Five warmups,
fresh provider creation for fresh modes, reused style/source for warm modes,
unique revisions for misses, warmed/uncontrolled OS file caches. External timings
include provider/canvas setup, request, publication and harness-covered disposal.
Both observers are enabled and output validation is outside timers.

| Direct-PNG request median | Main before → after | Repeat before → after |
| --- | --- | --- |
| Reused-provider miss | 90.95 → 68.81 ms (24.3% lower) | 89.66 → 68.67 ms (23.4% lower) |
| Fresh-provider miss | 112.28 → 95.33 ms (15.1% lower) | 117.12 → 98.31 ms (16.1% lower) |
| Cache hit using newly seeded entry | 3.13 → 2.68 ms | 3.10 → 2.55 ms |

Encoding/publication in complete misses drops from about 30 ms to 7.5–7.8 ms.
The cache-hit improvement describes newly encoded entries. Legacy entries use
their stored representation. Main fresh p95 rises 140.42 → 201.05 ms, while repeat
fresh p95 falls 153.07 → 125.99 ms; host/GC tails remain noisy. No universal tail
gain follows from these measurements.

An additional eight-case post-change matrix runs 20 samples/mode. All 1,920 measured
complete requests in this slice pass exact pixel checks and cache-publication
verification. The eight render reference PNGs match the preceding profile's
reference hashes. Cache payload bytes reflect the selected new compression.

## Validation and scope

- 158 tests pass. Eight new encoder cases cover BGRA/RGBA, premultiplied/
  unpremultiplied/opaque alpha, RGB565, Gray8, Alpha8, floating-point RGBA, sRGB and
  linear color spaces, exact legacy-decoded pixels and retained caller bitmap
  ownership. Additional cases cover mixed raster-style compact encoding and legacy
  cache bytes. Existing concurrent pruning, publication failure, corrupt PNG repair
  and cross-API cache/ownership tests pass.
- Restore-enabled full Release solution build passes for all four library targets
  and Windows demos. Solution tests and pack pass. Package includes the documented
  encoding policy and third-party notice.
- GitNexus index matches committed HEAD. Upstream `PublishPng` impact is CRITICAL,
  with both cache cores, Mapsui and Gmap flows. Source inspection confirms the
  shared encoder call sites. Whole pending-diff detection reports critical aggregate
  risk, 12 tracked files / 47 symbols / 43 flows, including earlier changes. New
  untracked encoder tests/benchmark files fall outside those graph counts.
- Pixel and timing evidence is CPU/Windows-specific. GPU surface activation,
  cross-platform native behavior, retained heap and native allocation totals remain
  outside this slice's measurements. Checked-in binary map/image assets were used
  locally; synthetic diagnostics are generated in memory.

## Local evidence and next step

Reproduce with the [benchmark README](../../VectorTileRenderer.Benchmarks/README.md).

- [Encoder main samples](../../artifacts/allocation-trace/png-encoding-comparison.json)
  and [repeat plus synthetic cases](../../artifacts/allocation-trace/png-encoding-repeat.json).
- [Encoder summary CSV](../../artifacts/allocation-trace/png-encoding-summary.csv).
- [Complete-request comparison CSV](../../artifacts/allocation-trace/png-encoding-request-comparison.csv).
- Main results in `artifacts/png-encoding-before/20261115-221859-a3593a6e` and
  `artifacts/png-encoding-after/20261115-222229-a7d4970b`.
- Repeat results in `artifacts/png-encoding-before-repeat/20261115-222510-f3c5727a`
  and `artifacts/png-encoding-after-repeat/20261115-222452-ca556ea9`.
- Eight-case post matrix in `artifacts/png-encoding-matrix-after/20261115-222531-451c57a6`.
- Baseline binaries in `artifacts/png-encoding-before-bin`; package in
  `artifacts/png-encoding-package`.

Next review/checkpoint the accumulated performance changes. Reprofile the current
request before selecting further preparation/filter/path work. Work remains
uncommitted; R-006 remains In progress.
