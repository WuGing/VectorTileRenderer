# GPU validation and alternatives to Skia

Proposal, 2026-09-05. Related: F-001, F-007, F-008; R-001, R-006, R-007. No alternative engine was installed or benchmarked in this audit.

## Recommendation

First validate host-owned Skia GPU rendering and measure complete tile requests. The present implementation does not establish that Skia's GPU renderer is slow or broken. It establishes gaps in this project's hosting, fallback reporting and readback checks. Replacing the rasterizer would also leave our style evaluator, label placement and disk cache in place.

If the priority is a complete interactive map, investigate MapLibre Native as an architectural alternative. If the priority is the current bitmap-tile API, compare a small Direct2D or Blend2D adapter only after profiling identifies drawing as material. Vello is a further exploratory GPU option, with interop and text integration work to budget.

## Candidate comparison

| Candidate | What it supplies | Fit and limitations / proposed investigation |
|---|---|---|
| Retain SkiaSharp, fix host integration | Existing CPU/GPU drawing and current API compatibility | Lowest migration scope. Establish current GL context, render-thread affinity, deterministic disposal and context reuse. Bitmap readback remains unless output changes. |
| MapLibre Native | A full native map renderer with documented Windows and other platform support | Strong candidate for full-map rendering and style/label pipeline evaluation; much larger integration than an ICanvas swap. Verify offline MBTiles resource delivery, .NET interop, native packaging and WPF presentation. |
| Direct2D + DirectWrite | Hardware-accelerated Windows 2D geometry, bitmap and text APIs | Practical Windows-specific spike. Existing style evaluation and map label collision still need ownership. Verify backend wrapper choice, headless behavior, pixel parity and readback cost. |
| Blend2D | CPU vector rasterization with JIT and multithreaded rendering | Useful CPU throughput comparison, not a GPU solution. Account for C/.NET interop, deployment, glyph/path text and copying into SKBitmap. |
| Vello | GPU compute-oriented 2D rendering | Exploratory adapter candidate. Verify chosen binding, target hardware, native packaging, glyph pipeline and complete bitmap-output performance rather than assuming a speedup. |

Primary sources checked 2026-09-05:
- [Skia canvas creation and current OpenGL context](https://skia.googlesource.com/skia/%2Bshow/22ae23891e8e6bc66bff018be4a1d647d43ec58a/site/docs/user/api/skcanvas_creation.md)
- [SkiaSharp GRContext.CreateGl API](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.grcontext.creategl?view=skiasharp-3.119)
- [MapLibre Native platforms](https://maplibre.org/maplibre-native/docs/book/platforms/index.html) and [Windows build](https://maplibre.org/maplibre-native/docs/book/platforms/windows/build-msvc.html)
- [Direct2D overview](https://learn.microsoft.com/en-us/windows/win32/direct2d/direct2d-overview)
- [Blend2D architecture](https://blend2d.com/about.html) and [threaded rendering](https://blend2d.com/doc/multithreaded-rendering.html)
- [Vello project](https://github.com/linebender/vello)

The fit assessments are project-specific inferences from these capabilities and the current ICanvas contract. None is a measured winner or a drop-in replacement.

## GPU correctness gate

Use deterministic project-authored geometry plus legally usable checked-in styles/fonts. Record OS, CPU/GPU, driver, runtime, package versions, host, thread IDs, output size and coordinates.

| Case | Required evidence |
|---|---|
| Cpu without GL context | Nonblank expected CPU pixels and actual backend CPU |
| Gpu/Auto without GL context | Explicit CPU fallback with reason; valid CPU output |
| Host-owned current GL context | Actual GPU surface, successful readback, pixel comparison against CPU with declared tolerance |
| First Auto probe fails, later valid host | Host-scoped/reprobe behavior defined and tested; current global negative cache exposed |
| GPU surface allocation/readback failure | Explicit result or fallback, never a successful-looking invalid bitmap |
| Async provider yields | Correct current context on the drawing continuation thread |
| Repeated renders, context loss and shutdown | Defined context/surface/bitmap disposal, recovery and bounded native memory |

A test that merely asks for Gpu and gets an image can pass on CPU and must not count as GPU validation. Record unsupported hardware as unvalidated, not passed.

## Performance gate

Measure 256, 512 and 1024 pixel tiles, geometry-heavy and text-heavy styles, warm/cold source and image caches, and fixed concurrency. Separate source read/decode, style evaluation, geometry, text, finish/readback, PNG encode/write and complete request latency. Include first-context startup, steady state, allocations and median/p95 latency. Existing DrawTextMs includes FinishDrawing and must not be used as a pure text measure.

Compare identical workloads with actual backend reporting. Keep a predeclared minimum worthwhile improvement and pixel-quality threshold in the experiment record. If CPU-side processing dominates, prioritize that work; if readback dominates, evaluate presentation/output architecture before swapping engines.

