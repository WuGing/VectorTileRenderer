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

For shared cache directories, supply a stable identity covering all bound datasets
and their revisions:

```csharp
using var bitmap = await Renderer.RenderCached(
    cachePath, style, canvas, x, y, zoom,
    sourceRevision: "regional-map:2026-11-06", sizeX: 256, sizeY: 256);
```

Change the token whenever any source data changes. The same token can be reused
across application restarts and provider instances. Tokens are hashed into filenames;
provider objects and file paths do not automatically identify revisions. Null (or the
original overload) keeps existing unversioned cache keys, so those callers must
partition cache directories by dataset/revision themselves. Empty is a distinct,
versioned token. Changing a token leaves older files in place; cache eviction remains
caller-managed. Corrupt PNG entries are treated as misses and repaired when writable.

Run explicit maintenance on a dedicated cache directory when convenient:

```csharp
var result = TileCache.Prune(cachePath,
    maxBytes: 512L * 1024 * 1024, maxAge: TimeSpan.FromDays(30));
```

This removes expired tiles, then oldest-written tiles until the byte limit is met.
Age uses last write time; cache hits do not refresh it. Zero bytes removes all eligible
tiles. Only top-level rendered-tile PNG names are eligible, including old revisions.
Unrelated names, nested directories, temporary files and file links are excluded;
linked directory paths are rejected. Matching a name does not prove file ownership:
use a directory reserved for rendered tiles and prevent external path replacement.

The returned DeletedFiles, DeletedBytes, RemainingBytes and FailedFiles describe
the observed eligible files; unknown sizes from metadata failures are excluded.
Locked/unwritable files may prevent meeting the limit. Directory enumeration errors
propagate. Pruning shares the in-process cache read/publication lock, so schedule this
synchronous disk operation away from latency-sensitive requests. Other processes
are not coordinated, and subsequent renders can grow the cache again. No automatic
maintenance is enabled; temporary-file scavenging remains caller-managed.

## Request profiling

`Renderer.ProfileSink` still reports successful renders. Existing `Backend` remains
the caller hint and `DrawTextMs` still includes finalization for compatibility.
Use `ActualBackend` (CPU/GPU/Unknown), `DrawTextOnlyMs` and `FinishDrawingMs` for
separate text and finish/readback measurements. GPU is reported only after a
successful finish with an enabled GPU surface; third-party canvases report Unknown.

`Renderer.CacheProfileSink` receives one `CacheRequestProfile` for each normally
completed RenderCached call, including cache hits and missing tiles. `CacheHit` and
`HasBitmap` distinguish those outcomes. `ActualBackend` is DiskCache on a hit,
CPU/GPU for known rendered canvases, or Unknown for missing/custom output.
`LookupMs` includes key construction, directory setup, lock waits, cache decoding
and corrupt-entry removal. `RenderMs` includes the complete inner Render call
(including its legacy profile callback and request resource cleanup).
`EncodeAndPublishMs` includes PNG encoding, write/publication waits and temporary
cleanup, including best-effort attempts that fail. A positive duration does not
prove persistence. `TotalMs` covers that cache request through completion, excluding
the cache-profile callback; hits have zero render/encode timings.

Both sinks are optional, process-wide callbacks that may run concurrently; use
thread-safe observers and clear them when finished. Observer exceptions are ignored.
Thrown requests emit no completed profile. These metrics exclude host queuing before
entry, caller encoding/disposal after return and GPU context creation outside the
call. No performance overhead or speedup claim is made by adding these measurements.

## Font fallback and shaping

List preferred fonts in the style and set FontDirectory explicitly. Ordinary covered
Latin text keeps its existing rendering path. Other text uses glyph-coverage checks,
configured/system fallback and HarfBuzz font/script shaping. Fallback preserves text
and combines measurement with the same glyph positions used for drawing.

Missing font coverage can still produce a missing-glyph box. Full bidirectional
paragraph layout across mixed-direction runs and coordinated cross-tile placement
remain incomplete. Native shaping has been exercised on Windows; other hosts need
native-dependency and output validation.
