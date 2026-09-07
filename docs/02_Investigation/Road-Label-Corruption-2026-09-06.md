# Road-label corruption: production fix

Date: 2026-09-06 (America/Denver; validation timestamp is September 7 UTC)
Baseline: `9abe693`; changes validated in the working tree.
Related finding: [F-003](Findings/F-003.md).

## Reproduction and causes

The user reports garbled road text in Mapsui.Demo.WPF and a consuming project.
Fresh CPU renders of the demo's Zurich location reproduce partial names inside
tiles, not just at boundaries. Two new raster tests failed before the fix:
a 60px road drew part of WEST 31ST AVE, and curved text differed from rigid,
positioned glyphs. Both now pass.

`SkiaCanvas.DrawTextOnPath` measured actual text width but used character count
multiplied by text size and 0.2 for fitting. This admitted roads too short for the
label. The selected SkiaSharp overload also defaults to warping outlines along
the path. See the installed version's [SKCanvas implementation](https://github.com/mono/SkiaSharp/blob/v3.119.2/binding/SkiaSharp/SKCanvas.cs).
This is a fix to our placement and API usage; a backend replacement is unnecessary.

The earlier orientation fix corrected length and bend checks and therefore could
admit more roads to these defective paths. That explanation is source-based;
we have not run a controlled pre-orientation raster comparison.

`LineClipper.ClipPolyline` also concatenates disconnected clipped segments.
Labels now use separate visible runs so they cannot follow an invented connector.

## Changes

- Require measured text width, offset and halo allowance to fit a continuous run.
- Center one rigid glyph blob on the road and reuse it for halo and fill.
- Preserve readable direction without modifying caller geometry.
- Reject labels whose bounds extend beyond the tile; reserve collision space only
  after acceptance, using the label bounds instead of the entire road bounds.
- Version the RenderCached key to avoid loading old corrupt PNGs. Old cache files
  remain on disk; no deletion or migration is required.

These shared library changes affect CPU/GPU rendering, all demos and consumers.
GitNexus was refreshed with `analyze --index-only`; upstream impact for
DrawTextOnPath reported 32 dependents and CRITICAL scope. Source inspection and
regressions cover the shared path. Public signatures are unchanged.

## Validation

- Full Release solution build: 0 warnings, 0 errors, including all four library targets.
- Full portable regression suite: 53/53 pass. Five new cases cover measured fit,
  rigid glyph pixels, disconnected runs, tile-edge clipping and legacy cache bypass.
- Library pack succeeded under `artifacts/road-label-package` (local validation
  package only; package version was not advanced or published).
- `dotnet run --project VectorTileRenderer.GpuValidation -c Release -- --labels . artifacts/road-labels-after`
  produced 12 fresh 3x3 grids / 108 tiles: basic and bright styles, zooms 12/14/16,
  CPU and verified texture-backed GPU with successful pixel readback.
- Host: Windows, .NET 10, NVIDIA GeForce RTX 4070 Ti, OpenGL 4.6 NVIDIA 616.56.
  No disk cache; a shared provider warms during the matrix. This is visual
  correctness evidence, not a timing benchmark or a CPU/GPU pixel-parity pass.
- Local before/after grids and JSON: `artifacts/road-labels-before` and
  `artifacts/road-labels-after`. Visually inspected basic z14/z16 CPU and bright
  z12/z16 GPU after images: complete readable street labels replace fragments.
  Inputs are the existing Zurich sample and bundled fonts. Images remain ignored
  developer artifacts under the [data licensing policy](../test-data-licensing.md).

The matrix uses the same renderer, dataset, center and styles as Mapsui, bypassing
its cache. Interactive Mapsui pan/zoom and the external consuming application have
not been exercised during this validation.

## Remaining work

F-003 stays open: Unicode shaping/fallback, point-label clipping, duplicate labels
across neighboring tiles and global collision/placement still need dedicated work.
A road too short or too close to an edge is omitted rather than partially drawn;
this deliberately reduces label density. Conservative bounds and sharp-turn checks
can omit otherwise placeable labels. Cross-tile road stitching could improve this
later. Existing CPU/GPU raster differences are outside this fix.

Rebuild and restart the demo to clear in-memory tiles. Consumers must reference
the updated build; an already installed package will not change automatically.
The Direct2D experiment remains paused while production text is prioritized.
