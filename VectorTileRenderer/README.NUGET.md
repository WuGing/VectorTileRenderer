# VectorTileRenderer

Vector map tile rendering library for .NET.

## Highlights

- Renders OpenMapTiles/Mapbox-style vector tile data.
- Supports MBTiles and PBF vector tile sources.
- Supports raster overlays for hybrid map rendering.
- Includes CPU and experimental GPU backend selection.

## Quick Start

```csharp
var style = new VectorTileRenderer.Style("styles/basic-style.json")
{
    FontDirectory = "styles/fonts/"
};

var source = new VectorTileRenderer.Sources.MbTilesSource("tiles/zurich.mbtiles");
style.SetSourceProvider("openmaptiles", source);

var canvas = VectorTileRenderer.CanvasFactory.Create(VectorTileRenderer.RenderBackend.Cpu);
var bitmap = await VectorTileRenderer.Renderer.Render(style, canvas, 1439, 1227, 13, 512, 512, 1);
```

## Backend Notes

- `RenderBackend.Cpu`: safest default.
- `RenderBackend.Gpu`: attempts GPU usage; falls back when unavailable.
- `RenderBackend.Auto`: probes GPU availability and falls back to CPU.

For complete examples and demo integrations, see the repository README and demo projects.
