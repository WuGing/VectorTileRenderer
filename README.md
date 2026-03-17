# VectorTileRenderer

## A comprehensive vector map tile renderer for .NET/C#

<p align="center">
  <img src="images/zurich.png">
</p>

Complete vector tile map rendering solution built in C# for .NET projects.

## Features

The library includes most of the components needed to build a map application:

- Ready-to-run demos for GMap .NET, Mapsui, and static maps
- Includes 7 styles: Basic, Bright, OSM Liberty, Dark, Runner, Street, and Hybrid
- Vector tiles can be loaded from an MBTiles database or PBF (protocol buffer) file
- Automatic decompression of gzipped PBF tiles
- Support and demos for WPF and WinForms
- Support for basic satellite and hybrid satellite raster tiles
- Plug-and-play support for multiple rendering engines
- Compatible with the [Mapbox/OpenMapTiles vector tile specification](https://www.mapbox.com/vector-tiles/specification/)
- Supports the [Mapbox style specification](https://www.mapbox.com/mapbox-gl-js/style-spec/)
- Stress free MIT License

![](images/zurich-basic.png) ![](images/zurich-bright.png) ![](images/zurich-light.png)


## So what is a vector map tile?

The maps that we see in online and offline applications are composed of blocks of square tiles. Each tile is an image, and the map is basically a grid of these images. In traditional apps, these images were in PNG/JPEG format, and are fetched from the server and displayed on the client.

Vector tiles are a newer concept and behave similarly to SVG. Instead of sending full PNG/JPEG image tiles, the server sends vector data composed of paths, polygons, points, and text. That vector data is rendered on the client side. This gives developers control over map appearance: road colors, water styling, feature visibility, and more. Vector tiles have the following advantages:

- They have a small size, and can save a lot of bandwidth.
- They are highly customizable in every aspect.
- They can be scaled up from 1x to ∞ without losing quality.
- They can be safely base rendered at 256x256, 512x512, or 1024x1024 sizes.
- They are ideal for offline maps.
- They can be used for geocoding and reverse geocoding.

## Basic Example

### Loading a tile from .pbf file:

```C#
// load style and fonts
var style = new VectorTileRenderer.Style("basic-style.json");
style.FontDirectory = "styles/fonts/";

// set pbf as tile provider
var provider = new VectorTileRenderer.Sources.PbfTileSource("tile.pbf");
style.SetSourceProvider(0, provider);

// render it on a skia canvas
var zoom = 13;
var canvas = new SkiaCanvas();
var bitmap = await Renderer.Render(style, canvas, 0, 0, zoom, 512, 512, 1);

imageView.Source = bitmap;
```

### Loading a tile from .mbtiles file:

```C#
// load style and fonts
var style = new VectorTileRenderer.Style("bright-style.json");
style.FontDirectory = "styles/fonts/";

// set mbtiles as tile provider
var provider = new VectorTileRenderer.Sources.MbTilesSource("zurich.mbtiles");
style.SetSourceProvider(0, provider);

var zoom = 13;
var x = 1439;
var y = 1227;

// render it on a skia canvas
var canvas = new SkiaCanvas();
var bitmap = await Renderer.Render(style, canvas, x, y, zoom, 512, 512, 1);

imageView.Source = bitmap;
```

 ![](images/zurich-hybrid-wo.png) ![](images/zurich-liberty.png) ![](images/zurich-dark.png)

### Merging vector and raster for hybrid satellite view:

```C#
// load style and fonts
var style = new VectorTileRenderer.Style("hybrid-style.json");
style.FontDirectory = "styles/fonts/";

// add vector tile
var vectorProvider = new VectorTileRenderer.Sources.PbfTileSource(@"tiles/zurich.pbf.gz");
style.SetSourceProvider(0, vectorProvider);

// add raster satellite tile
var rasterProvider = new VectorTileRenderer.Sources.RasterTileSource(@"tiles/zurich.jpg");
style.SetSourceProvider("satellite", rasterProvider);

// render it on a skia canvas
var zoom = 14;
var canvas = new SkiaCanvas();
var bitmap = await Renderer.Render(style, canvas, 0, 0, zoom, 256, 256, 1);

imageView.Source = bitmap;
```

 ![](images/hybrid.png)

## Vector Map in Mapsui and GMap .NET

The repository contains demos for showing vector tiles in both Mapsui and GMap .NET. The integration code is intentionally simple and easy to modify.

### Using vector mbtiles data in Mapsui

```C#
// load the source
var source = new VectorMbTilesSource(@"tiles/zurich.mbtiles", @"styles/basic-style.json", @"tile-cache/");

// attach it to the map
MyMapControl.Map.Layers.Clear();
MyMapControl.Map.Layers.Add(new TileLayer(source));
MyMapControl.Map.ViewChanged(true);
```

### Using vector mbtiles data in GMap .Net

```C#
// load the source
var provider = new VectorMbTilesProvider(@"tiles/zurich.mbtiles", @"styles/basic-style.json", @"tile-cache/");

// attach it to the map
gmap.MapProvider = provider;
```

## Using Mapbox Tiles and Styles

In terms of format and specifications, Mapbox tiles and styles are similar to OpenMapTiles tiles and styles. However, naming conventions and terminology differ, so they are not directly compatible for mix-and-match use. For Mapbox tiles, use Mapbox-compatible styles.

VectorTileRenderer supports both Mapbox and OpenMapTiles formats, but a compatible style must be used for rendering.

![](images/NY.png) ![](images/NY2.png)

## Known issues

The entire project, including demos, integrations, and optimization techniques, was developed rapidly and is not yet positioned as production-ready. While testing the demos, these are the main known issues:

- In some places, text may be cut off or distorted at edges.
- There is no mechanism yet to purge old cache entries in the `tiles-cache` directory.
- Rendering is SkiaSharp-based and mostly CPU-driven, so tile loading may show some lag.

## GPU powered vector map drawing

Rendering is currently SkiaSharp-based, with CPU as the most reliable default path.

The project now includes an experimental dual backend (`Cpu`, `Gpu`, `Auto`) so applications can choose runtime behavior. In practice, whether GPU is used depends on whether a valid GPU context can be created in the current host/thread environment.

Important context:

- In this project, tile fetch/decode/style evaluation is often the dominant cost, not just draw calls.
- The current pipeline returns `SKBitmap` tiles, so GPU paths still perform a readback step before returning.
- Because of those constraints, GPU mode is currently best treated as opportunistic rather than guaranteed faster.

Candidate future directions for deeper hardware acceleration include SkiaSharp GPU context hosting (OpenGL/ANGLE) and render-to-surface pipelines that avoid per-tile bitmap readback.

### CPU/GPU dual backend support

The renderer now supports selecting CPU or GPU canvas at runtime:

```C#
// CPU only
var cpuCanvas = VectorTileRenderer.CanvasFactory.Create(VectorTileRenderer.RenderBackend.Cpu);

// GPU preferred, falls back to CPU when no GPU context is available
var gpuCanvas = VectorTileRenderer.CanvasFactory.Create(VectorTileRenderer.RenderBackend.Gpu);

// Auto: try GPU first, then CPU fallback
var autoCanvas = VectorTileRenderer.CanvasFactory.Create(VectorTileRenderer.RenderBackend.Auto);
```

Notes:

- GPU mode uses a Skia GPU canvas and reads back to bitmap in `FinishDrawing`, so it is compatible with the existing bitmap output pipeline.
- If a GPU context cannot be created on the current host/thread, rendering falls back to CPU automatically.
- For offline tile generation and background workers, `Cpu` is generally the safest default.
- `Auto` probes GPU availability and falls back to CPU when unavailable.
- For production use today, benchmark your own workload and keep CPU as the baseline.

## Contribution

The project has strong potential and would benefit from ongoing contributions. Bug reports, suggestions, and pull requests are all welcome. Please submit them through the [GitHub issue tracker](https://github.com/WuGing/VectorTileRenderer/issues).

## License

This software is released under the [MIT License](LICENSE). Please read LICENSE for information on the
software availability and distribution.

Copyright (c) 2018 [Ali Ashraf](http://aliashraf.net)