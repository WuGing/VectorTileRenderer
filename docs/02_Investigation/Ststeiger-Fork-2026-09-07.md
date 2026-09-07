# ststeiger fork evaluation - 2026-09-07

Compared our `3a263ea` (preview.6) with ststeiger master
`a5a7749629a5d517bf769b82e590e7b71c7e4697` (latest inspected commit dated
2021-09-08). This is a source review with one runtime probe of our parser,
not a performance or visual bake-off between the two forks.

## Conclusion

Useful targeted lessons, especially invariant parsing, font coverage and native
resource ownership. Do not merge wholesale or replace our renderer. Their newer
VectorTileRenderer2 is referenced by RasterTileServer, targets netstandard2.0,
and uses SkiaSharp 2.80.3. The older VectorTileRenderer and OldVectorTileRenderer
projects also remain; folder names alone do not identify the active server path.

## Findings and recommendations

| Area | Source-confirmed difference | Recommendation |
|---|---|---|
| Color parsing | Fork uses InvariantCulture for numeric RGB/HSL components. Our ParseColorString uses ambient culture. Reproduced locale-dependent alpha/error in our code. | Adopt this small fix first with culture-specific regressions; see F-010. |
| Font coverage | FontManager enumerates Unicode code points including surrogate pairs and tests actual nonzero glyph indices. | Apply the principle to F-003. Character/glyph counts cannot establish font coverage. |
| Mixed scripts | GetBestMatchingFont takes text by reference, retains the supported prefix of words, and leaves complete script-run rendering as TODO. Plain DrawText remains the drawing API. | Do not copy truncation. Design fallback runs and shaping without losing text, with combining-mark, supplementary-character and mixed-direction fixtures. |
| Font configuration | GetFont returns a static Noto font before the style font-selection code. Static font-manager initialization scans hard-coded user download directories. Point-label bounds are measured before selecting the fallback font. | Preserve caller-selected fonts, configurable paths and matching measurement/drawing fonts. Their implementation is not a portable drop-in solution. |
| Ownership | Public Render owns a disposable SkiaCanvas and returns PNG bytes encoded before disposal. | Strong lesson for F-002: make result/canvas ownership explicit. Consider an additive encoded-output entry point after defining contracts; do not force PNG encode/decode into desktop bitmap rendering. |
| Disposal completeness | Canvas disposes bitmap/canvas and fontPairs, but Noto/static FontManager/FontInfo typefaces are outside that per-canvas cleanup. DrawImage creates SKData without a using scope. | Adopt ownership boundaries, not this exact cleanup implementation. No native-memory soak test was run. |
| Road labels | Both inspected SkiaCanvas versions return immediately from DrawTextOnPath. | They did not solve the road-placement defects. Preserve our tested glyph placement and curvature checks. |
| Point-label edges | VectorTileRenderer2 still checks containment only under ClipOverflow and estimates width from the longest string by character count. | Our recent measured-bounds fix addresses a gap still present there. |
| GPU | VectorTileRenderer2 creates a CPU SKBitmap/SKCanvas; GPU initialization is commented out. | No usable GPU activation/readback/parity solution found. Our validation work remains necessary. |
| HTTP server | ASP.NET Core raster endpoint wraps rendered PNG bytes, flips Y and sets HTTP cache headers. Uses hard-coded dataset/style and static shared state. Disk-cache reads/writes in GetTileStream are commented out. | Useful deployment example if we need a server, not evidence of improved renderer throughput or complete caching. |
| Tile-source concurrency | Fork has a Dictionary cache guarded by lock(key), where key is a newly constructed string each call, and blocks on .Result. | Do not adopt. Equal string values do not establish shared lock identity. Our ConcurrentDictionary/stable per-key lock implementation is structurally stronger. No race reproduction was attempted. |
| Performance rewrites | Replaces LINQ with a custom Enumerable implementation. TestRenderer is a WinForms executable, not a discovered automated regression suite. | No benchmark evidence found to justify wholesale replacements; retain targeted measurement. |
| Portability/dependencies | netstandard2.0 port and local support projects were useful historical work. Current repo already targets netstandard2.0/net7/net8/net10 and has portable tests. | No reason to downgrade packages or import vendored dependency trees. Review each dependency's notices before any future copying; no code imported here. |

## Pinned source evidence

- [New library project](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/VectorTileRenderer2/VectorTileRenderer2.csproj)
- [Server project references](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/RasterTileServer/RasterTileServer.csproj)
- [Culture-independent parsing, lines 692-741](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/VectorTileRenderer2/Style.cs#L692-L741)
- [Unicode codepoints and fallback, lines 133-300](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/VectorTileRenderer2/FontManager/FontManager.cs#L133-L300)
- [Noto priorities and exclusions](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/VectorTileRenderer2/FontManager/NotoFontManager.cs)
- [Per-font native objects](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/VectorTileRenderer2/FontManager/FontInfo.cs)
- [Font selection and point/path labels](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/VectorTileRenderer2/SkiaCanvas.cs#L368-L597)
- [CPU canvas setup](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/VectorTileRenderer2/SkiaCanvas.cs#L34-L58)
- [Encoded result and cleanup](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/VectorTileRenderer2/SkiaCanvas.cs#L680-L779)
- [Render owns canvas](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/VectorTileRenderer2/Renderer.cs#L129-L156)
- [Server endpoint and inactive disk cache](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/RasterTileServer/Controllers/TilesController.cs#L89-L187)
- [Tile cache locking](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/VectorTileRenderer2/Sources/MbTilesSource.cs#L194-L218)
- [Custom Enumerable](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/VectorTileRenderer2/Enumerable.cs)
- [Manual test application](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/TestRenderer/TestRenderer.csproj)
- [Repository license](https://github.com/ststeiger/VectorTileRenderer/blob/a5a7749629a5d517bf769b82e590e7b71c7e4697/LICENSE)

## Runtime probe and limits

Local ignored probe: artifacts/fork-culture-probe/Program.cs and Probe.csproj.
It calls our private static Style.ParseColorString via reflection to bypass the
color cache, parsing `rgba(100,150,200,0.5)` after setting CurrentCulture:

| Culture | Result |
|---|---|
| en-US | alpha 127, RGB 100/150/200 |
| de-DE | alpha 251, RGB 100/150/200 |
| fr-FR | FormatException |

An initial probe through ParseColor reused a cached value and concealed the defect;
the uncached probe establishes it. No production code changed. Suggested fix:
explicit InvariantCulture for all color-number parsing, plus fresh-style public
ParseStyle tests across these cultures and both RGB/HSL decimal inputs.

The fork was downloaded into artifacts/ststeiger-review, with sparse source checkout
excluding datasets/images/styles. No fork build or render was attempted: execution
would require its machine-specific font/dataset configuration and legacy dependency
setup. Commit names such as "Fixed Multi-Language capability" are not acceptance
evidence. No speed, memory, full Unicode or GPU correctness claims are inferred.

GitNexus refreshed our graph to current HEAD (2,215 nodes/5,771 edges). The font
query returned no flows with "FTS indexes missing - keyword search degraded".
Targeted source inspection supplies the evidence; the empty graph result does not
mean no callers. No index was built for the external fork.

## Next sequence

1. Fix and regress the reproduced locale bug (F-010), a small bounded change.
2. Resume F-002: explicit canvas/result/cache-writer ownership, then sustained
   repeated-render validation. Encoded output is an optional proposed API, not an
   implemented replacement for Render returning SKBitmap.
3. Resume F-003: genuine font coverage and lossless fallback/shaping with measured
   bounds, then coordinated cross-tile placement. Borrow the code-point awareness,
   not text truncation or hard-coded Noto configuration.
4. Defer HTTP hosting and broad LINQ rewrites until required and measured.

No remote issues, commits or production changes were made during this evaluation.
