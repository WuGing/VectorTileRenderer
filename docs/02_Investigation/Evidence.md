# Comment and known-issue inventory

Inspected 2026-09-05, commit `a4f8ca8`. Paths below are repository-relative; line numbers describe that baseline. These are source observations, not performance measurements.

## Explicit TODOs and unfinished branches

| Evidence | Interpretation / disposition |
|---|---|
| `VectorTileRenderer/Renderer.cs:11` — “make it instance based... maybe” | Design question: static ProfileSink and global cache lock exist; instance conversion is not itself a fix. H-001, Q-003. |
| `Renderer.cs:201` — “refactor this messy block” | Fetch/style/visual construction are interleaved. Preserve ordering, missing-tile semantics and per-render caches if extracting helpers. R-006. |
| `Renderer.cs:240,290` — exception performance comments | Throws after return/continue are unreachable. Document vector-null versus raster-skip behavior; remove dead code with focused tests in a later change. Not measured exception overhead. |
| `SkiaCanvas.cs:446` — “still causing issues”, “so we cut the rest” | QualifyTypeface truncates Brush.Text using glyph count; Unicode/script-run correctness needs a fixture. F-003. |
| `SkiaCanvas.cs:460` — symbol collision TODO | TextOptional branch has no implementation. Other rectangle collision checks do exist. F-003. |
| `SkiaCanvas.cs:609` — “buggggyyyyyy”, collision-system note | DrawTextOnPath clips paths, uses bounding rectangles and squeezing/length heuristics. Do not claim collision detection is entirely absent. F-003. |
| `SkiaCanvas.cs:682` — custom-function TODO | Halo still uses Skia DrawTextOnPath. A comment alone does not establish a Skia defect. F-003. |
| `SkiaCanvas.cs:693` — “draw icon here” | DrawPoint does not draw anything, including its IconImage branch. F-004. |
| `SkiaCanvas.cs:757` — empty DrawUnknown | Unsupported geometry is silently ignored; define diagnostic behavior. F-004. |
| `Style.cs:1182` — color mappings TODO | String stops choose nearest endpoint, not blended color. F-005. |
| `Style.cs:659,737` — color-format NotImplementedException | Unsupported/invalid color inputs can throw; distinguish invalid syntax from promised supported syntax. F-005. |
| `Style.cs:953` — “Comparing colors probably” | Comparison branch throws for SKColor operands. F-005. |
| `Style.cs:1234` — “Unimplemented interpolation” | Non-string/non-array/non-number stops throw. Build a supported-expression matrix. F-005. |

These are all five explicit TODOs and five NotImplementedException sites returned by the authored C# scan. Commented drawing/debug experiments in Renderer and SkiaCanvas are cleanup candidates, not ten separate feature requests. The commented screenScale multipliers in Style.ParseStyle merit zoom/DPI tests (Q-004), not automatic restoration.

## README known issues

| README note | Current assessment |
|---|---|
| Text cut off/distorted at edges | Existing known issue; code has related heuristics/truncation, but this audit did not reproduce screenshots. F-003. |
| No purge of old tiles-cache entries | Confirmed no eviction in RenderCached; caller supplies the actual directory. F-006. |
| CPU-driven lag | CPU path exists; lag and dominant cost are workload-dependent, unmeasured here. H-001. |
| Experimental Cpu/Gpu/Auto | Implemented selection; no dedicated GPU tests found. Selection/fallback is not proof of hardware rendering. F-001. |
| Fetch/decode/style “often dominant” | A claim requiring representative profiling; not established by this audit. H-001. |
| GPU bitmap readback | Confirmed OnBeforeFinishDrawing reads into SKBitmap; success ignored. F-001/F-007. |
| Plug-and-play engines | ICanvas still exposes SKBitmap. F-008. |
| Mapbox style support | Partial feature implementation; avoid implying full expression/symbol conformance. F-004/F-005. |

## Other source-supported leads

- Documentation QA found the root README's LICENSE target absent in this checkout;
  see Q-005. This was pre-existing; license text was not synthesized.

- RenderCached's background Task.Run shares the returned bitmap; caller disposal can race encoding (F-002). Exceptions are swallowed.
- The global cache lock covers disk decode and PNG encoding/writes, a possible concurrent throughput limit (H-001).
- Cache key uses style.Hash, dimensions, scale, layer whitelist and x/y/z, but no source revision/provider identity (F-006).
- SkiaCanvas/SkiaGpuCanvas have no IDisposable contract for their surfaces, contexts and cached typefaces (F-002).
- Renderer starts the canvas before awaiting providers; an asynchronous continuation needs the correct current GPU context (F-001).
- DrawTextMs includes FinishDrawing; the demo backend hint is resolved before surface creation can fall back (F-007).
- Gmap.Demo.WinForms/VectorMbTilesProvider.cs uses RenderCached(...).Result; examine caller scheduling before labeling it a UI deadlock. Q-003.
- Mapsui prefetch catches failures as best effort; measure wasted work and cancellation needs before changing it. H-001.
- Existing composite fixture task explicitly lacks visual seam evidence, despite synthetic routing tests. See [task](../composite-mbtiles-fixture-task.md).

## Reproduce the inventory

```powershell
rg -n -i 'TODO|FIXME|HACK|XXX|NotImplementedException|bug|performance|//.*\?' VectorTileRenderer Static.Demo.WPF Mapsui.Demo.WPF Gmap.Demo.WinForms VectorTileRenderer.Tests VectorTileRenderer.Benchmarks -g '*.cs'
npx --no-install gitnexus status
npx --no-install gitnexus query "GPU canvas rendering bitmap cache" --repo VectorTileRenderer --limit 3
npx --no-install gitnexus context SkiaGpuCanvas --repo VectorTileRenderer
```

GitHub observations and per-issue applicability are in [GitHub-Tracking](GitHub-Tracking.md).
