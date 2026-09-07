# GPU pixel-parity investigation

Investigated 2026-09-05 at `7bc45e1891402ab2fbda5a557ea94e3856fc9085` plus the
local label and GPU failure fixes. [F-009](Findings/F-009.md) records the finding.
[Machine-readable evidence](Gpu-Parity-2026-09-05-summary.json) contains source
hashes, environment, dataset metadata, isolated layers and control experiments.

## Conclusion

The two failing 256px cases reproduce with identical Skia drawing commands
replayed onto CPU and GPU surfaces. This localizes the differences to backend
rasterization/blending for these fixtures, rather than tile decoding, style
selection, path direction, missing GPU activation, or failed readback.

The synthetic case is dominated by antialiased strokes. Colorado combines dense
landcover, transportation and street-label edge differences. A separate flat
translucent fill also reproduces a small backend color difference. These are
observed output differences; the CPU reference is not an independent geometric
oracle, so this experiment does not establish which backend is more accurate
or prove a defect in a particular native Skia routine.

The original acceptance gates remain failed. Production renderer code and
tolerances were not changed during this investigation.

## Method and controls

Added an isolated `--parity` mode to the Windows GPU harness. It records the
library's actual canvas commands into SKPicture, then replays the same picture
onto CPU and GPU surfaces with identical 256px image format and alpha type.
It isolates source layers using Renderer's existing whitelist. This preserves
the background, and layers without visible features can therefore be blank.

All 19 captures reproduce production CPU bytes exactly. All 19 default-sampling
GPU replays reproduce production GPU bytes exactly. Texture-backed GPU snapshots
and readback are checked; each of the 57 GPU layer/sample replays is repeated and
must be byte-identical. The additional sample counts are requests to Skia, not
queried physical sample counts. No CPU fallback is accepted by replay.

Direct Skia controls bypass Renderer, tile providers, styles, label placement,
and coordinate conversion. They draw 40 known polylines with AA on/off, widths
1/2/4, and stroke versus precomputed filled outline. Opaque/translucent rectangles
test interiors separately. Metrics also identify CPU-flat 3x3 neighborhoods;
this is an edge-localization aid, not an independent shape correctness test.

Environment: Windows build 26200, .NET 10.0.11 x64, SkiaSharp package 3.119.2
(assembly 3.119.0.0), NVIDIA RTX 4070 Ti, OpenGL 4.6, driver 616.56.
Colorado: local 260,767,744-byte dataset, native TMS 14/3411/10167,
25,089 features / 156,288 geometry points. Reused decoded provider; no timings
or disk-cache performance conclusions in this diagnostic.

## Results

Mean error is over four RGBA channels, in 0–255 units. The original gate is
mean <= 1.5 and <= 1% of pixels with any channel difference > 32, plus a
foreground-content requirement.

| Isolated rendering, default sampling | Mean error | Pixels >32 | Interpretation |
|---|---:|---:|---|
| Synthetic complete | 1.99329 | 1.16119% | Fails both error limits |
| Synthetic roads | 2.34465 | 2.12860% | Dominant error; no changed CPU-flat interiors |
| Synthetic areas | 0.24405 | 0% | Small translucent-fill and edge differences |
| Synthetic labels | <0.01 | 0% | Not the synthetic failure driver |
| Colorado complete | 1.88075 | 0.60425% | Fails mean limit |
| Colorado landcover | 1.03953 | 0% | Dense small-geometry differences |
| Colorado transportation | 0.70082 | 0% | Many low-amplitude differences |
| Colorado street names | 0.54684 | 0.58746% | Largest high-contrast differences at text edges |

Layer errors are not additive: backgrounds, occlusion and blending change the
composite result. Every nonempty isolated Colorado source layer meets the error
limits even though the complete tile does not. Background-only layers can fail
the foreground requirement with zero pixel error; that is not a rendering defect.

The direct AA-on, width-2 Skia control reproduces the roads-only error
(mean 2.34465). AA-off drops it to 0.28221, but still has 180 differing boundary
pixels and a larger maximum difference (188). Turning AA off exchanges smooth
edges for jagged ones; it is not an acceptable parity fix. Filled outlines do
not improve the failing width-2/4 AA cases and make the width-1 AA case worse.

An integer-aligned opaque rectangle matches exactly. The same rectangle with
alpha 204 gives CPU interior `#ff89b798`, GPU `#ff89b998`: green differs by 2,
with all other channels equal. This independently explains the dominant small
interior delta in the synthetic translucent areas. It is a backend color/blend
result, not a geometry displacement or lost readback.

| Requested GPU samples | Synthetic complete mean / >32 | Colorado complete mean / >32 |
|---:|---:|---:|
| 0 (production-equivalent) | 1.99329 / 1.16119% | 1.88075 / 0.60425% |
| 4 | 2.22499 / 1.26495% | 1.98584 / 0.57678% |
| 8 | 1.68999 / 0.24872% | 1.81033 / 0.57678% |

Neither requested MSAA setting passes both complete fixtures. Increasing samples
is not a validated solution and its latency/memory cost was not benchmarked.

## Interpretation and next work

Skia documents GPU shape coverage, border antialiasing and blending as parts of
its generated shader pipeline; see [SkSL pipeline documentation](https://docs.skia.org/docs/user/sksl/).
That supports investigating backend edge/color behavior, but does not prove the
precise internal algorithm responsible in this installed native build. Our
command-replay and primitive controls are the direct evidence here.

Recommended next step: define a primitive-aware image acceptance policy using
independent geometry/coverage checks and separate backend reference images.
Keep explicit missing-feature, displacement, alpha and readback checks. A whole
image average changes with edge density and composition, so simply increasing
its threshold risks hiding a real missing feature. No new acceptance policy has
been implemented or approved by this report.

For closer raster matching, a focused Skia issue reproduction can use the
project-authored raw stroke and translucent rectangle cases without distributing
Colorado data. Native Skia algorithm tracing, other drivers/devices, alternate
GPU backends and performance of any remedy remain untested. The findings do not
justify replacing Skia or claiming all GPU output is correct.

## Reproduction and validation

```powershell
dotnet run --project VectorTileRenderer.GpuValidation -c Release -- --parity . artifacts/parity-investigation-final tiles/real/osm-2020-02-10-v3.11_us_colorado.mbtiles
dotnet build VectorTileRenderer.sln -c Release --no-restore
dotnet test VectorTileRenderer.sln -c Release --no-build --no-restore
```

Diagnostic exits 0 when the investigation completes with verified replay; this
does **not** mean the original image gate passes. Failures in replay/capture
assertions exit 1. Omit the dataset argument for synthetic-only reproduction.
Raw images/JSON remain ignored under artifacts; do not publish local map imagery
without resolving [provenance](../test-data-licensing.md).

Release solution build: zero warnings/errors. Portable regressions: 48/48 pass.
No production symbols changed in this investigation. GitNexus status matched
HEAD; keyword query reported missing FTS indexes. Qualified DrawLineString
context confirmed Renderer as caller; source verified the shared CPU/GPU method.
