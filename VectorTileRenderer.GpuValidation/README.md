# Windows GPU validation experiment

This executable exercises the production Renderer and SkiaGpuCanvas with a hidden,
host-owned WGL context. It is isolated from shipping demos and is not a replacement
backend or a production hosting API. It adds no NuGet dependencies beyond the library.

From the repository root, using .NET SDK 10 and a Windows x64 graphics session:

```powershell
dotnet run --project VectorTileRenderer.GpuValidation -c Release -- . artifacts/gpu-validation 30
```

Arguments are repository root, output directory, and measured iterations per case
(minimum 5; default 30). The output directory contains generated synthetic styles,
CPU/GPU PNGs, amplified difference PNGs, and results.json with raw timing samples.
The default artifacts directory is ignored by Git. No network data is required.

To replace the normal workload set with a developer-local MBTiles stress sample,
pass a fourth argument (use a separate output folder to preserve earlier evidence):

```powershell
dotnet run --project VectorTileRenderer.GpuValidation -c Release -- . artifacts/gpu-validation-colorado 30 tiles/real/osm-2020-02-10-v3.11_us_colorado.mbtiles
```

The database must contain vector PBF and TMS rows. Selection reads the largest
compressed tile at native zooms 10, 12 and the metadata maximum (deduplicated and
capped at that maximum). Ties use column/row order. Missing selected zooms are
skipped; no selected tiles is an error. This is a demanding sample, not exhaustive
coverage or a claim that compressed size equals rendering complexity. The report
includes actual coordinates, compressed size, decoded feature/point counts and
per-layer counts. No database is copied, rewritten or committed.

Each selected tile runs through SingleMbTilesSource in two modes: a reused provider
with its decoded cache primed, and a fresh provider per request including metadata
read, tile lookup and decoding. The latter is named fresh-source, not cold-disk:
OS and SQLite caching remain uncontrolled, and the selection scan warms file data.
Fresh-source disposal does not measure process-wide SQLite pool shutdown. Native
zoom sampling does not test overzoom or composite routing. Output sizes remain
256/512/1024 and all timing cases still require their own pixel-parity pass.

Exit 0 means the exercised correctness assertions and pixel gates passed; 1 means
a correctness assertion or pixel gate failed; 2 means hardware GPU support was
unavailable. GPU failure behavior is now enforced by `GpuFailureRegression`
assertions: failed readback must throw repeatedly, a new render after abandonment
must fall back correctly, and Auto must recover across context/thread changes.
The earlier AutoAfterInitialFailure and AbandonedReadback observations remain in
historical reports. Do not run this automatically as a hardware-independent CI test.

## What is measured

### Isolated parity diagnosis

```powershell
dotnet run --project VectorTileRenderer.GpuValidation -c Release -- --parity . artifacts/parity-investigation tiles/real/osm-2020-02-10-v3.11_us_colorado.mbtiles
```

Omit the final dataset argument for synthetic-only analysis. This mode captures
production Skia commands and checks CPU/default-GPU replays against production,
isolates source layers, repeats GPU output, requests sample counts 0/4/8, and runs
direct stroke/opacity controls. It does not benchmark. Exit 0 means diagnostic
execution completed with verified replay, **not** that the original pixel gates
pass. Unsupported hardware/replay failures exit 1 in this diagnostic mode.
Background-only isolated layers can fail the foreground gate with zero error.
See [results and limitations](../docs/02_Investigation/Gpu-Parity-2026-09-05.md).

### Standard correctness and timing run

- No-context factory selection and CPU pixel output.
- Actual texture-backed GPU output and an independent, checked readback that must
  match the bitmap returned by production code exactly.
- CPU/GPU image comparison at 256, 512 and 1024 pixels. Tolerance is fixed at mean
  absolute RGBA channel error <= 1.5/255 and <= 1% of pixels with any channel error
  > 32/255. CPU output must also contain more than 1% non-background pixels.
- A yielding provider with and without a synchronization-context pump. An ICanvas
  guard rejects wrong-thread native calls before invoking Skia. Renderer currently
  swallows geometry exceptions, so the rejection count is also recorded.
- Skia context abandonment, allocation fallback, host teardown and three host
  recreations. Abandonment is not a physical driver reset.
- Per-case timings only when that case passed the image gate: 5 warmup pairs, then
  alternating CPU/GPU pairs, concurrency 1, with a shared host and reused canvases.

The synthetic geometry/text workloads are project-authored. Zurich uses the
existing tiles/zurich.pbf.gz, styles/basic-style.json and existing fonts in place;
no new map/font assets are redistributed. See docs/test-data-licensing.md.

The decoded Zurich case reuses parsed data. The file-decode case reads, decompresses
and parses the file each request, but its OS file cache is uncontrolled/warm; it is
not a physical cold-disk benchmark. Style objects, fonts and the GPU context are
warm/reused. Context creation is reported separately. Current demos can have a
different per-tile lifetime, so these results do not establish demo performance.

RenderMs covers Renderer.Render through FinishDrawing. TotalMs also includes PNG
encoding and synchronous file write/close (no durable flush). ImageCacheHitMs is a
separate PNG decode with no rendering. This uses an explicit synchronous cache
operation, not production RenderCached's fire-and-forget writer or global lock.
It does not measure UI presentation, network, concurrent contention, or physical
disk latency. Slight CPU/GPU image differences can affect PNG encoding cost.

The production profile's text bucket includes FinishDrawing. The harness records
finish and readback separately and subtracts finish from that bucket. Checked
verification readback is disabled during timing to avoid doubling transfers.
ManagedBytes is current-thread allocation only; native/GPU allocations are excluded.
Private-memory snapshots after forced finalization are observations, not proof of
a steady-state bound. A 10% total-latency improvement was chosen before measurement
as the minimum worthwhile result for this first experiment.

## Ownership and limits

WindowsGlContext owns HWND/HDC/HGLRC on the main thread. RenderThread pumps captured
await continuations there. GRContext is host-owned and borrowed by ProbeCanvas;
surfaces and bitmaps are released before the context and native host. The harness
releases old surfaces between renders. The base class's private cached typefaces
and per-draw native allocations still have the production lifetime limitations
tracked in R-002; this harness does not claim to repair them.

CPU/GPU/difference images are evidence for a fixed fixture, not general Mapbox/text
conformance. No tolerance is widened when a case fails. Unsupported hardware is
reported as unvalidated. WPF/Mapsui integration, multiple GPUs, remote desktop,
macOS/Metal and actual device removal remain separate validation work.

See [the measured report](../docs/02_Investigation/Gpu-Validation-2026-09-05.md).
The [Colorado report](../docs/02_Investigation/Gpu-Colorado-2026-09-05.md) covers the
larger developer-local MBTiles experiment.

## Road-label visual matrix

`dotnet run --project VectorTileRenderer.GpuValidation -c Release -- --labels . artifacts/road-labels-after`

Renders fresh 3x3 Zurich grids using the Mapsui center, basic/bright styles,
zooms 12/14/16, CPU and verified GPU readback. Requires the existing Zurich sample,
bundled fonts and the same Windows GPU host as the other experiments. Outputs
PNGs and hardware/tile metadata; visually inspect the images. Exit zero proves
completion, not automated text quality or CPU/GPU pixel parity. See the
[production fix report](../docs/02_Investigation/Road-Label-Corruption-2026-09-06.md).

## Bounded placement timing

`--label-timing <root> <output> <mbtiles> [iterations=30] [workload-name-filter] [size]` measures complete CPU requests on the largest compressed native tiles at zooms 10, 12 and maximum. Five warmups precede individual request/text/decode timings. Final images are saved outside timing. Warm/fresh providers are separate; OS caches are uncontrolled. Run before/after binaries sequentially without competing builds.

The `--labels` matrix now includes zoom 18 and accepts optional latitude/longitude after the output directory. See the [placement report](../docs/02_Investigation/Road-Placement-2026-09-06.md).
