# Street-label orientation fix

Inspected 2026-09-05 at baseline `7bc45e1891402ab2fbda5a557ea94e3856fc9085`;
implementation and tests are working-tree changes. Related finding: [F-003](Findings/F-003.md).

The Colorado experiment already reproduced upside-down street labels on both
backends. The user's report from another consuming project is consistent with
that reproduction; that project's particular style/data has not been replayed.

## Cause and change

[`SkiaCanvas.DrawTextOnPath`](../../VectorTileRenderer/SkiaCanvas.cs) passed the
stored road direction directly to Skia. Right-to-left geometry could therefore
produce upside-down glyphs. The clipped label path now reads left to right, with
exactly vertical paths reading bottom to top. Clipping creates a separate list,
so road geometry owned by the caller is not reversed.

The bend check also compared the first segment against a fixed zero angle,
skipped the final segment, and calculated wrapped angles incorrectly. It now
compares consecutive nonzero segments with a wrapped angular difference. The
existing 60-degree rejection threshold remains. Length now includes every
segment, allowing ordinary two-point roads to receive labels.

This changes label visibility and therefore collision outcomes as well as
orientation. No public signature or backend contract changed. Both CPU and GPU
inherit this path; Renderer and the three desktop consumers are affected.

GitNexus status matched HEAD. Upstream impact on the qualified DrawTextOnPath
symbol reported the GPU subclass, but incorrectly described inheritance as an
override. Source inspection verified the Renderer call and shared implementation.
`detect-changes --scope all` reported high risk across the accumulated 14 tracked
files and six flows, including earlier GPU/documentation work. It does not cover
untracked tests/harness files. Full build, raster regressions, and real-data
CPU/GPU checks address the shared-rendering risk.

## Validation

- Four raster cases failed before the fix and passed afterward: horizontal
  two-point, gently curved, vertical, and duplicate-point roads. Tests require
  visible pixels, identical forward/reverse output, unchanged input, and upright
  horizontal pixels above the baseline. Additional tests reject a sharp final
  turn and zero-length geometry. See [PathLabelTests](../../VectorTileRenderer.Tests/PathLabelTests.cs).
- `dotnet test VectorTileRenderer.sln -c Release --no-restore`: 46/46 passed.
- Release solution build: zero warnings/errors, all four library targets and
  Windows examples. Release package and symbol package generated successfully.
- GPU harness rerun with five measured iterations against Colorado and the
  checked-in Zurich samples. Real GPU surfaces and checked readback succeeded.
  These are correctness reruns, not replacements for the earlier performance baseline.
- Inspected Colorado CPU/GPU 512px images: WEST 31ST AVE. is upright on both;
  additional street labels now appear. Inspected Zurich CPU 512px output too.
- Existing parity failures remain: Colorado z14 at 256px (mean channel error
  1.88075 versus 1.5 limit) and synthetic geometry at 256px. Both harness runs
  exit 1 for those gates. Colorado z14 at 512px/1024px passes; tolerances unchanged.
- WPF demo launched on its default checked-in Zurich basic-style view as a
  process smoke check. Visual evidence comes from offscreen renderer outputs;
  interactive demo screenshots were not captured.

Local before images: `artifacts/gpu-validation-colorado/` and
`artifacts/gpu-validation-final/`. After images and raw JSON:
`artifacts/label-fix-colorado/` and `artifacts/label-fix-zurich/`.
Colorado comparison file stem: `mbtiles-z14-3411-10167-warm-source-512-`,
followed by `cpu.png` or `gpu.png`. These ignored artifacts retain local map data;
do not publish them without resolving [provenance](../test-data-licensing.md).

## Remaining scope

F-003 stays open for multilingual truncation, clipping, collision and symbol
placement. Endpoint orientation is a bounded fix for ordinary road paths;
closed loops and paths that double back need label-span placement, not just
endpoint reversal. Mapbox keep-upright style configurability is not added here.
GPU Auto/readback defects and parity differences remain separate work.
