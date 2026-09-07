# Bounded road-label placement - 2026-09-06

Related finding: [F-003](Findings/F-003.md).
Baseline: 9abe693 plus local road/point-label fixes through cache version 3.

## Implementation

The user authorized prudent readability improvements with performance checks.
New private helper `SkiaCanvas.SelectLabelSection` scans each continuous clipped
road once, partitioning at turns over 15 degrees or total absolute bending over
35 degrees. It selects the least-bent section long enough for the complete name,
breaking ties by length. Direction is normalized before selection. Only the chosen
section gets a glyph blob; complete-fit, halo, collision and tile-edge checks stay.
Cache version 4 bypasses earlier output. Public signatures are unchanged.

The added selection is linear per visible run, with one selected-section allocation;
the pre-existing sort of clipped runs remains. There is no exhaustive glyph/candidate
search. Thresholds are readability choices. Greedy partitioning can miss usable
placements across a partition boundary; some labels are omitted or less centered.
Cross-tile placement, duplicates and Unicode remain open under F-003.

GitNexus status matched committed HEAD; upstream impact confirmed shared rendering,
GPU and demo scope. Graph line mappings predate local changes, so source and tests
are authoritative. detect-changes covers the cumulative local fixes.

## Correctness

Before the change, raster tests reproduced abrupt bending and failure to use a
straight stretch before a sharp corner. Tests now cover those cases, accumulated
curvature, gentle curves and reversed geometry. Full Release build: zero warnings
and errors across all library targets/demos. NUnit: 66/66 pass. Cache regression
covers legacy versions 0, 2 and 3. Library packaging succeeds.

Local before/after matrices: `artifacts/placement-visual-before` and
`artifacts/placement-visual-after`. Each has 144 fresh Zurich tiles, basic/bright,
zooms 12/14/16/18, centered at 47.373/8.542 near the user's reported streets.
CPU and texture-backed GPU/readback were exercised on the RTX 4070 Ti host.
Visually inspected basic z16/z18 CPU: readable gentler placement, including
Wohllebgasse and Strehlgasse. This is offscreen production rendering, not an
interactive Mapsui test or a CPU/GPU parity acceptance change. Images/data remain
local ignored artifacts per the existing licensing policy.

## Performance

Windows/.NET 10; Intel64 Family 6 Model 183 Stepping 1; CPU backend, sequential
complete Renderer.Render requests. Existing 249 MiB Colorado MBTiles, basic style,
largest compressed tiles at native zooms 10/12/14, 256/512px. Warm decoded source
and fresh source measured separately. Includes fetch/decode, style evaluation,
geometry/text and FinishDrawing; excludes PNG encoding and disk image cache hits.
OS caches/background load uncontrolled. Five warmups then 30 measurements, two
before/after batches. Baseline binaries were captured before production edits.
The first baseline briefly overlapped a test build; the repeat did not.

Pooled medians (60 samples per implementation/case):

| Tile/provider | Size | Before ms | After ms | Change |
|---|---:|---:|---:|---:|
| mbtiles-z10-213-635-warm-source | 256 | 51.85 | 47.05 | -9.3% |
| mbtiles-z10-213-635-warm-source | 512 | 50.50 | 49.11 | -2.8% |
| mbtiles-z10-213-635-fresh-source | 256 | 73.40 | 72.78 | -0.9% |
| mbtiles-z10-213-635-fresh-source | 512 | 77.22 | 76.22 | -1.3% |
| mbtiles-z12-853-2541-warm-source | 256 | 76.19 | 74.61 | -2.1% |
| mbtiles-z12-853-2541-warm-source | 512 | 80.89 | 78.75 | -2.6% |
| mbtiles-z12-853-2541-fresh-source | 256 | 120.83 | 130.13 | +7.7% |
| mbtiles-z12-853-2541-fresh-source | 512 | 124.61 | 125.99 | +1.1% |
| mbtiles-z14-3411-10167-warm-source | 256 | 64.00 | 63.97 | -0.0% |
| mbtiles-z14-3411-10167-warm-source | 512 | 64.88 | 67.83 | +4.6% |
| mbtiles-z14-3411-10167-fresh-source | 256 | 139.11 | 145.30 | +4.4% |
| mbtiles-z14-3411-10167-fresh-source | 512 | 146.48 | 149.96 | +2.4% |

The +7.7% fresh z12/256 case prompted a focused 60-request rerun, using identical
profiling harnesses with captured baseline/current libraries. Warm request medians
were 83.82 -> 85.76 ms; fresh were 136.85 -> 136.05 ms. Text-pass medians were
0.882 -> 0.873 ms warm and 0.850 -> 0.795 ms fresh. Fresh decode medians were
40.406 -> 40.665 ms. Phase medians do not sum to request medians.

No repeatable meaningful text-placement penalty was demonstrated. Mixed batch
results are retained; this is not proof of zero overhead on other data/hardware,
nor a GPU performance measurement.

Raw samples: `artifacts/placement-before`, `placement-after`, their `-repeat`
directories, and `placement-profile-before` / `placement-profile-after` (all under
artifacts). The focused JSON includes TextMs/DecodeMs arrays. An aborted stale
harness run produced no timing JSON and is excluded. Reproducibility DLL hashes:

- `artifacts/placement-baseline-bin/WuGing.VectorTileRenderer.dll`: `a1050a4749235ddbaba284aaaec394a40b46c72835f1f103321753e5c838ef1b`
- `VectorTileRenderer.GpuValidation/bin/Release/net10.0-windows/WuGing.VectorTileRenderer.dll`: `c51d443c95d81c4455a4b098b9e58a07fb91f502248a3a162578159ea3b4607d`

Commands:

```powershell
dotnet run --project VectorTileRenderer.GpuValidation -c Release -- --label-timing . artifacts/placement-after tiles/real/osm-2020-02-10-v3.11_us_colorado.mbtiles 30
dotnet run --project VectorTileRenderer.GpuValidation -c Release -- --labels . artifacts/placement-visual-after 47.373 8.542
```

Rebuild/restart the demo and update consuming references to use the new library.
No commit, remote issue change or package publication performed.
