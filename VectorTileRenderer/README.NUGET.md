# VectorTileRenderer

Vector map tile rendering library for .NET.

## Release notes - 0.1.1-preview.6

- Fixed upside-down, warped and truncated road labels; use intact glyphs and measured text width.
- Prefer gently bending road sections and reject unreadable placements while preserving tile-edge protections.
- Fixed area/building label cutoffs using measured multiline bounds, alignment, offsets and halos.
- Invalidate older rendered-tile cache entries so corrected labels appear after upgrading.
- Fixed sticky Auto CPU fallback and propagate GPU pixel-readback failures.
- Added label regressions and CPU/GPU visual and performance validation tooling.
- GPU support remains experimental; cross-tile label placement and Unicode shaping/fallback remain incomplete.

Rebuild and restart applications after upgrading. Labels that cannot fit fully within
a tile are omitted; coordinated placement across tiles remains future work.

## Highlights

- Renders OpenMapTiles/Mapbox-style vector tile data.
- Supports MBTiles and PBF vector tile sources.
- Supports raster overlays for hybrid map rendering.
- Includes CPU and experimental GPU backend selection.

## Quick Start

```csharp
var style = new WuGing.VectorTileRenderer.Style("styles/basic-style.json")
{
    FontDirectory = "styles/fonts/"
};

using var source = new WuGing.VectorTileRenderer.Sources.SingleMbTilesSource("tiles/zurich.mbtiles");
style.SetSourceProvider("openmaptiles", source);

using var canvas = new WuGing.VectorTileRenderer.SkiaCanvas();
using var bitmap = await WuGing.VectorTileRenderer.Renderer.Render(style, canvas, 1439, 1227, 13, 512, 512, 1);
```

Multiple regional databases can be exposed as one source. Requests are routed
using MBTiles coverage metadata and fall through when a matching database does
not contain the requested tile:

```csharp
using var source = new WuGing.VectorTileRenderer.Sources.CompositeMbTilesSource(
[
    "tiles/region-a.mbtiles",
    "tiles/region-b.mbtiles"
]);

style.SetSourceProvider("openmaptiles", source);
```

## Backend Notes

- `RenderBackend.Cpu`: safest default.
- `RenderBackend.Gpu`: attempts GPU usage; falls back when unavailable.
- `RenderBackend.Auto`: probes GPU availability and falls back to CPU.

For complete examples and demo integrations, see the repository README and demo projects.

## Canvas and bitmap ownership

Dispose each SkiaCanvas when finished and dispose every bitmap returned by Render
or RenderCached. FinishDrawing transfers bitmap ownership: the completed bitmap
remains valid after canvas reuse or disposal. A canvas is a single-threaded render
object. For CanvasFactory.Create, retain the ICanvas reference and dispose it via
IDisposable when supported. Supplied GRContext instances are borrowed; dispose the
canvas on its render thread before disposing the context. Contexts created by
SkiaGpuCanvas.TryCreate are owned by that canvas.

RenderCached finishes encoding and publishes a complete cache file before returning;
a first cache miss therefore includes encode/write time. Cache hits load caller-owned
bitmaps. Existing cache files are bypassed when rendering behavior changes.

## Font fallback and shaping

List preferred fonts in the style and set FontDirectory explicitly. Ordinary covered
Latin text keeps its existing rendering path. Other text uses glyph-coverage checks,
configured/system fallback and HarfBuzz font/script shaping. Fallback preserves text
and combines measurement with the same glyph positions used for drawing.

Missing font coverage can still produce a missing-glyph box. Full bidirectional
paragraph layout across mixed-direction runs and coordinated cross-tile placement
remain incomplete. Native shaping has been exercised on Windows; other hosts need
native-dependency and output validation.
