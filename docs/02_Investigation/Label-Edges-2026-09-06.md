# Bright label edge cleanup — 2026-09-06

Baseline: 9abe693 plus the uncommitted [road-label fix](Road-Label-Corruption-2026-09-06.md).
Related finding: [F-003](Findings/F-003.md).

## Reproduction

The user confirmed the road-label improvement but reported remaining area/building
label cutoffs in Bright, possibly also on roads. `SkiaCanvas.DrawText` only checked
containment when ClipOverflow was true (overzoom). Its estimated rectangle ignored
alignment and text offsets, selected the longest line by character count instead
of ink width, and did not match the actual multiline baselines.

Five synthetic raster cases failed before this cleanup: all four tile edges at
native zoom and an offset label at overzoom. All now pass. Two interior-label
controls ensure the fix still draws text; two additional alignment cases cover
left/right justification. Existing road edge and rigid-glyph regressions pass.

## Implementation and scope

Measure each line with the drawing font, translate its ink bounds using the actual
baseline, alignment and offset, union the bounds, and include halo/antialias margin.
Require the complete label to fit the actual tile at every zoom. Collision bounds
use the same placement with the existing five-pixel spacing. Reserve collision
space only for accepted labels. RenderCached version 3 bypasses both original and
road-fix-only cached PNGs; regression coverage tests both keys.

No road geometry/straightening change was needed for the reproduced point-label
defect. Roads continue to follow local tangents. This does not implement cross-tile
stitching or coordinated placement: an edge label may disappear entirely rather
than render a fragment. That tradeoff avoids visibly broken text but reduces label
density. Duplicates, Unicode shaping/fallback and better placement remain open.

GitNexus status matched HEAD, and qualified DrawText impact reported HIGH scope,
nine dependents. Its inherited-method results and omission of interface dispatch
are not a complete caller list; source verifies the shared CPU/GPU implementation
also serves all demos and consuming projects. Public signatures are unchanged.
The index describes committed source; current source/tests verify local edits.

## Validation

- Full Release solution build: zero warnings/errors, all four library targets.
- Full NUnit suite: 63/63 pass.
- Fresh matrix: 108/108 Zurich tiles, basic/bright, zooms 12/14/16, CPU and actual
  GPU surfaces/readback. Same RTX 4070 Ti/OpenGL host and fixture policy as the
  preceding report. No pixel-parity or performance claim.
- Before images: `artifacts/road-labels-after`; after: `artifacts/label-edges-after`.
  Visually inspected Bright z16 GPU and z14 CPU: point labels no longer break at
  internal tile edges. Road labels remain readable. Images are local ignored
  artifacts, not new redistributable fixtures.
- Library packaging succeeds; no package publication or version advancement.
- Interactive Mapsui and the external consumer were not automated. Rebuild/restart
  the demo and update the consuming library reference to use the corrected code.

Reproduce with:

```powershell
dotnet run --project VectorTileRenderer.GpuValidation -c Release -- --labels . artifacts/label-edges-after
```
