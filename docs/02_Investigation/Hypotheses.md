# Performance hypotheses

## H-001: Pipeline and cache costs may outweigh GPU drawing savings

Status: Open
Priority: High
Area: Rendering, Caching, GPU / Hosting
Related concerns:
Related findings: F-001, F-006, F-007
Confidence: Medium

Statement:
Fetch/decode/style, global cache locking, per-tile context setup and readback may dominate latency enough that GPU drawing does not improve complete tile requests.

Why this is suspected:
- README calls out CPU-side work; RenderCached holds a global lock during decode/encode/write.
- GPU output requires readback; contexts are created per canvas rather than by a persistent host owner.

What still needs to be verified:
- Which costs dominate representative cold and warm workloads.
- Whether contention, allocations or prefetch are material under realistic concurrency.
- Whether GPU is active and produces valid output.

Evidence so far:
- Renderer.RenderCached and RenderProfile; SkiaGpuCanvas.TryCreate/OnBeforeFinishDrawing.
- Existing benchmarks cover isolated operations, not proof of GPU speed.

Next validation step:
- Execute R-001 and R-006 with identical tiles/styles, fixed concurrency, CPU baseline, actual GPU state, allocation data and median/p95 complete-request latency. Record readback and PNG/cache time separately.

