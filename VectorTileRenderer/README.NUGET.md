# VectorTileRenderer

Vector map tile rendering library for .NET.

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

var canvas = WuGing.VectorTileRenderer.CanvasFactory.Create(WuGing.VectorTileRenderer.RenderBackend.Cpu);
var bitmap = await WuGing.VectorTileRenderer.Renderer.Render(style, canvas, 1439, 1227, 13, 512, 512, 1);
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
