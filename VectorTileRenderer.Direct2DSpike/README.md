# Direct2D / DirectWrite feasibility spike

Windows x64, .NET 10; hardware-specific, non-packable executable. Uses pinned
Vortice.Direct2D1/Direct3D11 3.8.3 bindings. It is an experiment, not a supported
backend or a new CanvasFactory option. The shipping library has no Vortice dependency.

```powershell
dotnet run --project VectorTileRenderer.Direct2DSpike -c Release -- . artifacts/direct2d-spike 30 tiles/real/osm-2020-02-10-v3.11_us_colorado.mbtiles
```

Arguments: repository root, output directory, measured iterations (minimum 5),
optional local PBF/TMS MBTiles file. Omit the database for synthetic/Zurich only.
The largest selected native-zoom warm-source case supplements the shared fixtures.
Output includes PNGs and JSON with hardware, compatibility results and raw samples.
Keep local map output ignored; see [provenance](../docs/test-data-licensing.md).

Exit 0 means the spike completed its hardware/control checks and recorded its
compatibility outcomes, **not** that it is safe to replace Skia. Exit 1 indicates
a build-independent runtime/check failure, including unavailable hardware.
`FullCompatibility.Completed` means no explicitly unsupported operation was met;
inspect its pixel comparison and images too. It is not map/style conformance.

## Experimental contract

`Direct2DCanvas` is an internal ICanvas implementation returning SKBitmap, with
IDisposable ownership. It creates a hardware-only D3D11 device (no WARP fallback),
a Direct2D device/context, reusable BGRA premultiplied target and CPU-read staging
bitmap. EndDraw, copy and map errors fail the request. Row-pitched pixels are
copied into a caller-owned SKBitmap. The canvas disposes its native resources and
private DirectWrite font collections on its owning thread. No host UI is needed.

Uses checked-in font files through private DirectWrite collections; fonts are not
installed. Point labels are single-line. Experimental LTR path labels shape with
DirectWrite, position glyph outlines along the road, normalize direction and draw
halos. This duplicates a subset of the current label heuristics and is not ready
for production. It explicitly rejects wrapped point labels, bidi/sideways labels,
icons, raster images, unknown geometry and overzoom polygon clipping. It does not
provide general style, font fallback, effects or symbol support. Review the images:
text parity remains a migration blocker even for completed synthetic workloads.

Unimplemented operations are accumulated and rejected at FinishDrawing, since
Renderer currently catches some geometry exceptions. Symbol points without an
icon are intentionally left to the separate text pass. Line clipping is linked
from the existing library; linked legacy code retains its nullable-analysis policy,
and the new adapter opts into nullable analysis.

## Measurement limits

Geometry-only rows whitelist fill/line source layers and exclude symbol layers.
They are not full map timings, including when the source fixture is named
synthetic-text. They require nonblank, repeatable output and the existing CPU-image
gate for Direct2D. Skia GPU rows are diagnostic baselines; its earlier parity
limitations remain and must not be treated as adoption approval.

Each case has five warmup rounds and 30 default measured rounds; backend order
rotates across rounds. Render time includes Renderer, draw submission and readback.
Total includes Skia PNG encoding and synchronous file write/close, without durable
flush. There is no production RenderCached writer, UI presentation, network or
concurrent request workload. Providers/fonts/devices are warm; OS cache is uncontrolled.

Direct2D reuses its target/staging surfaces at a fixed size; the existing Skia
ProbeCanvas recreates surfaces per request. Direct2D creates brushes/path/stroke
resources per draw. Results compare these adapters, not optimal native engines.
DirectWrite currently draws glyph outlines, so its results are not measurements
of an optimized glyph-atlas renderer. Native cache/lifetime work remains for Skia.

Shared workloads, CPU/GPU probes, readback comparison, GL host and thread pump are
linked from GpuValidation to avoid forked fixture definitions. Hardware checks are
not part of the portable NUnit suite.

See the [measured investigation](../docs/02_Investigation/Direct2D-Spike-2026-09-05.md).
