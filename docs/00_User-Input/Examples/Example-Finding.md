<!-- Illustrative record; IDs are examples, not live links. -->

# F-007: Canvas resources and asynchronous cache writes lack explicit ownership

Status: Confirmed
Priority: High
Area: Resource Ownership, Caching
Related concerns: C-014
Related hypotheses: H-003 
GitHub Issue:
Confidence: High

Summary:
SkiaCanvas retains surface/canvas/typefaces; SkiaGpuCanvas retains GRContext. Neither ICanvas nor these canvas classes exposes deterministic disposal. RenderCached schedules encoding using the same bitmap it returns.

Conclusion:
Native-resource lifetime is implicit, and the cache worker can outlive the caller-owned bitmap.

Evidence:
- VectorTileRenderer/ICanvas.cs; VectorTileRenderer/SkiaCanvas.cs:7-35,762-783; VectorTileRenderer/SkiaGpuCanvas.cs:7; VectorTileRenderer/Renderer.cs:105-132.

Impact:
Potential native memory accumulation and disposal/encoding races. Sustained growth or crash is not measured here.

Scope:
CPU/GPU canvases, repeat renders, missing-tile early returns, cache writer.

Recommendation:
Define ownership of returned bitmap versus canvas/context; dispose temporary resources and copy or await the cache writer's owned image.

Suggested refactor items:
- R-004 (illustrative)

Open questions:
- Q-004 (illustrative)

Notes:
Source inspection at a4f8ca8 on 2026-09-05; no runtime reproduction in this audit. See docs/02_Investigation for live records.

