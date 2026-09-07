# Fork lessons implemented - 2026-09-07

Baseline: our 3a263ea (preview.6); authorized follow-up to the
[ststeiger evaluation](Ststeiger-Fork-2026-09-07.md). Changes remain local.

## Implemented

1. **F-010, invariant colors:** all numeric RGB/HSL parsing uses InvariantCulture.
   Public ParseStyle regressions use fresh styles under en-US/de-DE/fr-FR with
   RGBA and HSLA decimals. German wrong-alpha and French exceptions reproduced
   before the fix; all three locales now pass.
2. **F-002, ownership:** SkiaCanvas implements IDisposable, releases abandoned
   frames before reuse, and transfers its completed bitmap from FinishDrawing.
   Returned bitmaps remain caller-owned after canvas reuse/disposal. Temporary
   paths/paints/fonts are scoped; cached typefaces are released on disposal.
   SkiaGpuCanvas borrows constructor-supplied GRContext, but owns contexts it
   creates through TryCreate. Failed factory candidates are disposed. Demos now
   dispose canvases and outputs; prefetch also disposes its unused bitmap.
3. **Cache writes:** RenderCached encodes before returning, writes a unique
   temporary file, then atomically moves it into place. Concurrent writers keep
   one complete entry and remove their temporary files. IO/permission failures
   leave a usable rendered result. This replaces fire-and-forget encoding of a
   caller-owned bitmap; it intentionally includes cache-miss write time in the call.
4. **Raster streams:** provider streams remain alive across all layers sharing a
   source and are disposed at request end, including exceptions/early returns.
5. **F-003, coverage/fallback:** replace count-based truncation with ContainsGlyphs
   checks. Resolve whole text elements using configured fallback fonts, then system
   matching. Font caches include the configured font directory. Unknown coverage
   retains the original text, possibly displaying .notdef; Brush.Text is not mutated.
6. **Shaping:** new internal TextLayout groups font/script runs and uses the matching
   SkiaSharp.HarfBuzz 3.119.2 dependency. The same shaped glyphs and positions drive
   measured bounds and drawing for point/path text. Existing covered ordinary Latin
   labels retain their fast path and raster regressions. Curvature, full-fit and
   tile-edge checks remain. Cache version 5 bypasses earlier label/color output.

These implement the useful principles, not a wholesale fork import. No server,
custom LINQ library, vendored dependency tree, dependency downgrade or PNG-only
replacement for the public bitmap API was introduced. Explicit existing ownership
contracts meet our desktop needs without forcing encode/decode into every Render.

## Contracts and remaining limits

ICanvas signatures are unchanged. SkiaCanvas adds IDisposable and FinishDrawing is
virtual for validation subclasses. Consumers must dispose each returned SKBitmap
and their SkiaCanvas separately. Skia surfaces are released after successful finish;
subclasses needing surface readback should use OnBeforeFinishDrawing. Native GPU
context operations and disposal remain thread-affine. Existing production GPU host
integration limitations are not solved here.

Fallback/shaping is not a complete multilingual layout engine: full Unicode bidi
paragraph reordering across mixed-direction runs, missing installed font coverage,
font-specific/color glyph edge cases and coordinated cross-tile labels remain.
No text is intentionally truncated for coverage. OS font availability can change
fallback output. Windows native shaping was exercised; other OS runtimes need their
own native-library/font validation. F-003 remains Confirmed, not Closed.

F-002 is Mitigated: concrete ownership and cache races addressed, but external
consumers must adopt disposal and a long-duration native-memory plateau was not
proven. GPU geometry parity F-009 remains unchanged.

## Validation

- Full regression suite: **81/81 pass**, including actual bundled-font fallback
  (Metropolis lacks Cyrillic Ж; OpenSans covers it), whole surrogate/combining
  elements, unchanged caller text, Arabic shaped glyph pixels, caller-owned bitmaps,
  500 repeated renders, atomic concurrent cache publication and shared raster lifetime.
- Full Release solution and four library targets build with zero warnings/errors.
- Actual CPU/GPU Zurich matrix: **144 tiles** plus two Unicode panels. Correct GPU
  surface/readback checked. Basic/bright, zooms 12/14/16/18, center 47.373/8.542.
  Local outputs: artifacts/fork-improvements-visual. Inspected Unicode CPU and
  bright z18 GPU images; fallback characters, joining and emoji tail text visible.
- GPU failure suite: **8/8 checks pass**, including borrowed-context reuse after
  canvas disposal. Overall GPU harness exit 1 remains expected because the existing
  synthetic-geometry 256px parity gate fails (mean 1.9933, 1.1612% over32 pixels).
  No tolerance was widened. Outputs: artifacts/fork-improvements-gpu.
- Repeated-render private-byte observation: 262,668,288 to 278,913,024 bytes in that
  harness run; this is not a plateau or leak-free certification.
- Package includes the matching HarfBuzz dependency; no package published/version
  advanced in this follow-up. Native execution verified on .NET 10/Windows x64,
  RTX 4070 Ti/OpenGL 4.6 NVIDIA 616.56.

## Performance

Isolated committed baseline copied into artifacts/fork-improvements-baseline,
then sequential old/new CPU runs. Basic style, largest compressed Colorado z14
TMS 3411/10167, 249 MiB dataset; 30 measurements after five warmups. No PNG cache;
warm and fresh providers separate, OS cache/background activity uncontrolled.
Full Renderer.Render request medians (ms):

| Provider/workload | Size | Before | After |
|---|---:|---:|---:|
| mbtiles-z14-3411-10167-warm-source | 256 | 70.21 | 63.17 |
| mbtiles-z14-3411-10167-warm-source | 512 | 69.81 | 67.12 |
| mbtiles-z14-3411-10167-fresh-source | 256 | 145.73 | 148.49 |
| mbtiles-z14-3411-10167-fresh-source | 512 | 146.24 | 141.34 |

No material rendering slowdown demonstrated in this sample; no universal speedup
claim. Raw samples: artifacts/fork-improvements-timing-before and -after.

Separate alternating warm-provider z14/256 probe: Render median about 64.87 ms;
RenderCached cache-miss median about 74.53 ms. Thirty measurements each after
warmups; every new cache file decoded successfully immediately after return.
The roughly 10 ms difference includes PNG encode/publication, which was previously
background work. This is the explicit safety/latency tradeoff, not free work.
Raw probe/results: artifacts/cache-ownership-probe. Cache-hit latency not measured
in this focused probe.

## Tooling and follow-up

GitNexus impact identified shared demo/render paths (DrawText CRITICAL, 45 dependents;
RenderCached HIGH). Source verified actual behavior. Prior FTS degraded-query
limitation remains; detect-changes checks cumulative scope. New helper types are
internal; no interface-breaking dependency on IDisposable was imposed on third-party
ICanvas implementations. Contributor and package usage guidance describe disposal.

Next: consuming-app validation, mixed-direction paragraph design and cross-tile
placement; retain existing GPU parity work. No remote issues, commit or publication.
