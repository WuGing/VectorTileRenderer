# Large local MBTiles GPU experiment — Colorado

The 248.69 MiB Colorado database did not produce a meaningful GPU advantage in
this bitmap-output pipeline. Across 16 eligible tile/size/cache cases, no median
render-plus-PNG speedup reached the predeclared 1.10x threshold. Reusing the decoded
tile cache mattered much more than backend selection. This supports continued CPU
baseline work; it does not establish a whole-database or interactive-map benchmark.

Related: F-001, F-002, F-003, F-006, F-007, H-001, R-001, R-002, R-006.

## Inputs and selection

User requested testing the larger developer-local files in tiles/real. Inventory:
Colorado 248.69 MiB, Idaho 209.34 MiB, Nevada 182.87 MiB, Utah 121.32 MiB. Selected
Colorado as the largest. Other datasets were inventoried, not rendered in this run.

File: `tiles/real/osm-2020-02-10-v3.11_us_colorado.mbtiles`, 260,767,744 bytes.
Metadata identifies OpenMapTiles v3.11, vector PBF, TMS rows, zoom 0–14, and
MapTiler / OpenStreetMap contributor attribution. No source data or rendered map
images were committed or published. Local usage follows the existing
[data policy](../test-data-licensing.md); stored attribution is preserved in the
[JSON summary](Gpu-Colorado-2026-09-05-summary.json).

The experiment selects the largest compressed tile at zooms 10, 12 and 14. These
are deliberate stress samples, not representative averages. Compressed size is
only a selection heuristic; decoded counts establish the actual workload.

| Zoom | Column | TMS row | Compressed bytes | Features | Geometry points |
|---:|---:|---:|---:|---:|---:|
| 10 | 213 | 635 | 103,247 | 9,450 | 22,456 |
| 12 | 853 | 2541 | 166,981 | 14,766 | 34,835 |
| 14 | 3411 | 10167 | 384,584 | 25,089 | 156,288 |

At zoom 14, counts include 8,853 building features, 7,919 landcover features,
5,444 housenumber features and 1,872 transportation features. Feature counts are
decoded input counts; style filtering may prevent some from being drawn.

The database contains 113,761 zoom-14 tiles. A read-only EXPLAIN QUERY PLAN for
the production coordinate lookup reported indexed searches through map_index
(zoom_level, tile_column, tile_row) and images_index (tile_id). Per-request lookup
does not read the entire 249 MiB database. The initial largest-tile selection scan
is outside timing and warms storage caches.

## How to repeat

Library baseline: `7bc45e1891402ab2fbda5a557ea94e3856fc9085`. Same Windows build
26200, .NET 10.0.11 / SDK 10.0.400, i7-13700K, RTX 4070 Ti and NVIDIA 616.56
environment as the [initial experiment](Gpu-Validation-2026-09-05.md).

```powershell
dotnet run --project VectorTileRenderer.GpuValidation -c Release -- . artifacts/gpu-validation-colorado 30 tiles/real/osm-2020-02-10-v3.11_us_colorado.mbtiles
```

The run ended at `2026-09-05T18:03:49.0796425Z`. Source/input SHA-256 fingerprints,
hardware, coordinates, parity metrics and all timing medians/p95 are preserved in
the [summary](Gpu-Colorado-2026-09-05-summary.json). Full samples and CPU/GPU/diff
PNGs remain local under `artifacts/gpu-validation-colorado/`.

Uses the production SingleMbTilesSource, Renderer.Render, SkiaCanvas drawing and
SkiaGpuCanvas readback with a hidden host-owned WGL context. Shipping library and
demos are unchanged. The added optional MBTiles argument is documented in the
[harness README](../../VectorTileRenderer.GpuValidation/README.md).

Two provider modes:

- **Warm source:** reused SingleMbTilesSource with the selected tile's decoded
  cache already populated. This is a repeated request, not a new-tile traversal.
- **Fresh source:** constructs/disposes SingleMbTilesSource each request, including
  metadata, lookup and decoding. OS/SQLite caches remain warm/uncontrolled. This
  is not cold physical disk, nor a realistic continuous pan with one provider.

Each eligible case uses 5 warmup pairs and 30 measured pairs, alternating CPU/GPU
order, concurrency 1. Host context/canvases/styles/fonts are reused. Total includes
render, PNG encode and synchronous file write/close, not durable storage or UI.
Image-cache hits are separately measured and do not invoke either backend. The
production fire-and-forget RenderCached writer is not part of these timings.

## Correctness

All 18 backend-comparison observations had actual texture-backed GPU surfaces and
successful independently checked readback. Eight of nine distinct tile/size image
combinations passed the unchanged tolerance. Both cache modes reproduce the same
pixels, giving 16 passes and 2 failures across their 18 observations.

The zoom-14 256px image failed mean RGBA channel error: **1.5351/255** against a
maximum of 1.5/255. Large pixel errors were **0.1648%**, within the separate 1%
limit. Both zoom-14 512px and 1024px cases passed, with means 1.1570 and 0.6342.
No thresholds were relaxed; the failed 256px case was not timed. Exit code **1**
is retained. These parity results do not establish cartographic correctness.

Visual inspection of the zoom-14 512px CPU/GPU images showed upside-down street
labels in both (for example WEST 31ST AVE near the lower left). This is shared
text-path behavior, consistent with F-003, not evidence of a GPU-only problem.
The amplified difference image mainly highlights fine geometry/text edges.

The harness also re-exercised its synthetic yielding-provider guard, abandonment,
CPU fallback and three host recreations. These lifecycle checks use the synthetic
fixture, not the large MBTiles provider, which currently completes synchronously.
Auto remains stuck after an initial failed probe; abandoned readback still returns
a production bitmap despite failed checked readback. Those shipping defects remain.

## Measured results

Zoom-14 heavy tile, milliseconds (median). Total includes PNG encode/write:

| Provider | Pixels | CPU render | GPU render | CPU total | GPU total | CPU/GPU total ratio |
|---|---:|---:|---:|---:|---:|---:|
| Warm source | 512 | 66.585 | 69.580 | 95.866 | 94.173 | 1.018x |
| Warm source | 1024 | 78.136 | 83.043 | 158.005 | 147.796 | 1.069x |
| Fresh source | 512 | 142.076 | 141.847 | 171.957 | 166.944 | 1.030x |
| Fresh source | 1024 | 158.935 | 159.099 | 238.239 | 224.579 | 1.061x |

For zoom 10 and 12 at 512px:

| Zoom | Provider | CPU total | GPU total |
|---:|---|---:|---:|
| 10 | Warm source | 58.178 | 61.748 |
| 10 | Fresh source | 86.570 | 89.941 |
| 12 | Warm source | 111.156 | 118.060 |
| 12 | Fresh source | 157.441 | 161.480 |

All sizes and p95 values are in the JSON summary. The strongest total speedup was
1.069x (zoom 14, warm source, 1024px), below 1.10x. PNG encoding cost differs with
the slightly different raster output; GPU rendering alone was generally slower
or approximately tied. These exploratory samples do not establish statistical
significance for small differences.

For the heavy 512px tile, provider reuse reduced median CPU render from 142.08 to
66.59 ms and total from 171.96 to 95.87 ms. Fresh-source fetch/metadata/decode took
66.53 ms CPU / 70.97 ms GPU; warm-source fetch was about 0.001 ms. This confirms
the value of the provider cache already present in the library, not a new missing
cache feature or an excuse to leave it unbounded.

Warm-source 512px CPU/GPU buckets: style 10.71/13.26 ms, geometry 24.81/21.04 ms,
text excluding finish 1.37/1.12 ms, finish 0.001/6.86 ms (GPU production
readback portion 1.02 ms), PNG encode/write 29.87/24.59 ms. Bucket medians do not
sum to the median total; other visual-layer work is not separately exposed.
Independent verification readback is disabled during timings.

Current-thread managed allocations were about 28.69 MB/request warm and 79.03 MB
fresh, for either backend; native/GPU allocation is excluded. Settled private
memory increased from 412,397,568 to 437,194,752 bytes (~23.65 MiB) over 1,120
warmup/measured operations. This does not prove a stable bound or isolate a leak.

## Implications and limits

Large datasets strengthen the case for explicit resource ownership and bounded
decoded caching, followed by profiling style/visual-layer construction and PNG
encoding. A different rasterizer would not remove those costs. The database uses
indexed tile lookup; its total size is not equivalent to one render's work.

Next work remains R-001/R-002: explicit failed-readback handling and host/resource
lifetime contracts, plus the failed image gate and shared label orientation.
Then test a sequence of distinct tiles to measure cache growth and realistic pan
latency, including bounded concurrency. That has not been measured here.

The test does not cover every Colorado tile, the other states, a genuinely cold
disk, multi-source seams, overzoom, production demo responsiveness, network loading,
other graphics hardware or device removal. Full Release solution build passed
with 0 warnings/errors, NUnit 40/40 passed, and packaging succeeded after the
MBTiles harness extension. No production fix or dataset redistribution occurred.
