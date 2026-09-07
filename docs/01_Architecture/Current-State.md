# Current rendering pipeline

Inspected 2026-09-05 at `a4f8ca8`.

The reusable library targets netstandard2.0, net7.0, net8.0, and net10.0; its project currently sets x64. WPF/WinForms demos host the library; NUnit tests and BenchmarkDotNet experiments are separate projects.

`Style` loads style JSON and binds providers. `Renderer.Render` starts an `ICanvas`, awaits vector/raster sources, evaluates styles and builds visual layers, then draws geometry and text. `FinishDrawing` returns `SKBitmap`. `RenderCached` adds PNG disk caching and schedules background encoding.

`CanvasFactory` selects `SkiaCanvas` or `SkiaGpuCanvas`. The GPU canvas wraps a `GRContext`, attempts a GPU surface, and reads pixels into an SKBitmap. It does not establish a native host OpenGL context. CPU and GPU share text and style behavior.

Working-tree follow-up on 2026-09-05: Auto now probes each calling context rather
than retaining process-global availability. Failed GPU snapshot/readback throws
and propagates through Renderer; a new request is needed for CPU recovery.
See [validation and remaining limits](../02_Investigation/Gpu-Failure-Fixes-2026-09-05.md).

`SingleMbTilesSource` handles one database; `CompositeMbTilesSource` routes among coverages with priority and fallback. The legacy `MbTilesSource` is obsolete. Existing synthetic tests cover composite routing; visual boundary fixtures remain an explicit separate task.

The backend boundary is partially abstracted: drawing operations use ICanvas, but output remains Skia-specific. A replacement engine must bridge SKBitmap or propose a compatible output-contract migration.

Evidence: `VectorTileRenderer/Renderer.cs`, `ICanvas.cs`, `CanvasFactory.cs`, `SkiaCanvas.cs`, `SkiaGpuCanvas.cs`, `Sources/`, and `VectorTileRenderer.csproj`.

