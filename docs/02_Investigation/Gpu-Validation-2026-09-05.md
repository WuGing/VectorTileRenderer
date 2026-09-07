# GPU validation — 2026-09-05

The isolated Windows host successfully rendered on an NVIDIA RTX 4070 Ti. GPU
surfaces were texture-backed and checked readback matched the production bitmap.
However, one of twelve CPU/GPU image comparisons failed the fixed tolerance, and
the experiment reproduced the cached Auto fallback and unchecked failed-readback
defects. None of the eleven eligible timing cases reached the predeclared 1.10x
complete-request speedup. Keep CPU as the baseline; this is not production GPU
acceptance or a reason to replace Skia yet.

Related: F-001, F-002, F-007, H-001, R-001, R-002, R-006. R-001's first validation
experiment is complete; shipping changes and the remaining image gate are open.

## Reproduction and provenance

Library baseline: `7bc45e1891402ab2fbda5a557ea94e3856fc9085`. The library and demos
were not modified. Added `VectorTileRenderer.GpuValidation` to the solution as an
explicit Windows x64 executable, with no new external package dependencies.

```powershell
dotnet build VectorTileRenderer.sln -c Release --no-restore
dotnet test VectorTileRenderer.sln -c Release --no-build --no-restore
dotnet pack VectorTileRenderer/VectorTileRenderer.csproj -c Release --no-build --no-restore -o artifacts
dotnet run --project VectorTileRenderer.GpuValidation -c Release --no-build -- . artifacts/gpu-validation-final 30
```

Final measurement ended at `2026-09-05T15:32:13.6674536Z`, with no overlapping
agent build/test work. Full JSON samples and CPU/GPU/amplified-difference PNGs are
under `artifacts/gpu-validation-final/` (ignored by Git). A compact
[checked-in result summary](Gpu-Validation-2026-09-05-summary.json) preserves the
measurements and observations. See the [harness README](../../VectorTileRenderer.GpuValidation/README.md)
for arguments, ownership and timing definitions.

Hardware/software:

- Intel Core i7-13700K; 24 logical processors.
- NVIDIA GeForce RTX 4070 Ti; driver/OpenGL string `4.6.0 NVIDIA 616.56`.
- Hardware pixel format, vendor `NVIDIA Corporation`; no GDI software fallback.
- Windows build 26200; x64 .NET runtime 10.0.11; SDK 10.0.400.
- SkiaSharp package 3.119.2 (assembly reports 3.119.0.0).
- WGL host creation: 143.31 ms; Skia GRContext creation: 1.98 ms in the final run.

## Correctness and failure behavior

| Case | Observed result |
|---|---|
| Cpu/Gpu/Auto without native context | Each factory selection chose CPU; null-context canvas rendered opaque CPU output. |
| Auto first fails, valid context added later | Still CPU. The negative result is cached process-wide even though hosted GPU drawing now works. |
| Host-owned GPU surfaces | All 12 normal image cases used texture-backed GPU surfaces with successful independent readback. Verification bitmap exactly matched production output. |
| Pixel comparison | 11/12 passed. Dense synthetic geometry at 256px failed. |
| Yielding provider with pump | Finished on the owning thread with successful readback. |
| Yielding provider without pump | Guard rejected wrong-thread native access, with 189 rejected calls. Renderer swallows geometry exceptions; FinishDrawing's guard ultimately surfaces the error. The experiment did not deliberately execute invalid native GL calls. |
| Context abandoned after surface creation | Production returned a bitmap, while independent ReadPixels returned false. This reproduces the missing failure check without a physical device reset. |
| Surface creation after abandonment | Fell back to CPU and returned the expected solid color. |
| Shutdown/recreation | Context unbound at shutdown; three recreated hosts rendered with successful GPU readback. |

The parity gate requires mean absolute RGBA channel error <= 1.5/255 and <= 1%
of pixels with any channel error > 32/255. CPU output must have > 1% non-background
pixels. Dense geometry at 256px produced mean error **1.9933** and **1.1612%** large
pixel errors. Tolerances were not relaxed. Visual inspection of CPU/GPU/difference
images suggests line-edge coverage differences; it does not establish their cause
or make this case accepted. Its performance measurement was skipped.

The geometry cases at 512/1024px, all synthetic text cases, and all Zurich cases
passed. Repeated runs retained the same pass/fail outcomes. The final synthetic fixture explicitly requires the checked-in OpenSans Regular.ttf; it does not rely on an OS font fallback. General font/script,
style conformance, adjacent-tile seams and WPF presentation are not established.

The executable returned **1**, preserving the failing image gate. This is a
deliberate reported validation failure, not a successful GPU suite or a regression
introduced into the library. The Auto/readback observations independently remain
production defects, regardless of image-tolerance results.

## Timings

Milliseconds, median of 30 measured samples per backend after 5 warmup pairs;
CPU/GPU order alternates, concurrency 1. Render includes production Renderer.Render
and FinishDrawing. Total adds PNG encode plus synchronous file write/close. It
does not include disk durability, UI presentation, or production RenderCached's
background writer/global lock. Canvases, fonts and host context are reused.

| Workload | Pixels | CPU render | GPU render | CPU total | GPU total | CPU/GPU total ratio |
|---|---:|---:|---:|---:|---:|---:|
| synthetic-geometry | 512 | 1.024 | 2.381 | 10.445 | 10.749 | 0.972 |
| synthetic-geometry | 1024 | 1.884 | 6.170 | 28.415 | 29.727 | 0.956 |
| synthetic-text | 256 | 0.311 | 1.272 | 2.479 | 3.407 | 0.728 |
| synthetic-text | 512 | 0.635 | 1.987 | 7.643 | 8.946 | 0.854 |
| synthetic-text | 1024 | 1.248 | 4.137 | 23.323 | 26.075 | 0.894 |
| zurich-decoded | 256 | 25.764 | 28.803 | 32.340 | 35.084 | 0.922 |
| zurich-decoded | 512 | 28.040 | 31.705 | 45.836 | 48.422 | 0.947 |
| zurich-decoded | 1024 | 41.986 | 43.034 | 123.446 | 116.524 | 1.059 |
| zurich-file-decode | 256 | 74.469 | 76.176 | 81.489 | 82.170 | 0.992 |
| zurich-file-decode | 512 | 74.466 | 77.544 | 92.342 | 93.926 | 0.983 |
| zurich-file-decode | 1024 | 89.646 | 88.578 | 171.336 | 159.990 | 1.071 |

Ratios above 1 favor GPU. No case reached 1.10x; pure Render medians favor CPU in 10 of 11 cases (the remaining difference is small). Small differences in GPU/CPU images also change PNG encoding
cost, so the faster 1024px total does not prove faster GPU rendering.

At Zurich file/decode 512px, CPU/GPU median source read/decompress/parse time was
43.38/42.90 ms; style evaluation 4.41/6.74 ms; geometry 9.42/7.76 ms; text excluding
finish 2.43/1.75 ms; finish 0.001/3.64 ms (GPU readback portion 1.88 ms); PNG
encode/write 17.96/16.68 ms. Individual bucket medians do not add to total medians,
and the profile does not separately expose every visual-layer construction cost.
Complete-request p95 was 131.72/106.36 ms. Current-thread managed allocations were
about 44.25 MB per request for both backends; native/GPU allocations are excluded.

The same 512px fixture's separate PNG-cache-hit decode median was 7.05/6.61 ms;
cache hits do not render on either backend. File/decode workloads re-read and parse
the PBF each request, but OS file cache was warm/uncontrolled. Decoded cases reuse
parsed data. These are exploratory measurements, not cold-disk or concurrency
benchmarks. The measurements support investigating decode/allocation and PNG/cache
costs before assuming a rasterizer replacement will improve complete requests.

Across the 770 warmup/measured render-plus-PNG operations, settled process private
memory increased from 277,032,960 to 292,585,472 bytes (~14.83 MiB). Managed
collection/finalization was forced before both snapshots. Caches, native objects
and driver allocations are not isolated, so this neither proves a leak nor a
stable bound. R-002 resource-lifetime work remains necessary.

## Validation and remaining work

- Full Release solution: 0 warnings/errors, including all four library targets
  and all Windows demos.
- Existing NUnit regression suite: 40/40 passed, no skips.
- Library nupkg and snupkg packaging succeeded.
- Hardware experiment: actual GPU/readback verified; 11/12 pixel gates passed;
  failed case retained, with no benchmark for that case.
- GitNexus index refreshed with --force --index-only. Impact traversal returned
  partial results with a read-only pool error; source confirmed Mapsui callers.
  No shipping symbol was edited. No commit or GitHub issue was published.

Next, make failed readback an explicit error and define host/context ownership
together with R-002. Replace process-global Auto availability with a host-aware
policy. Keep the 256px geometry comparison as a regression target and investigate
its edge differences before declaring parity. Then measure any fixes using this
harness. A production host API must explicitly define borrowed versus owned
context, render-thread scheduling, bitmap ownership and context-loss behavior.

The harness demonstrates those ownership choices internally; it does not add a
public API, fix production disposal, or integrate GPU presentation into Mapsui/WPF.
No other GPU/driver, remote-desktop mode, macOS/Metal or real device-removal event
was validated. The existing global-cache writer was intentionally not used for
timing because its work outlives the method return.

API references used for the experiment: [WGL current-thread contract](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-wglmakecurrent),
[pixel-format hardware/software flags](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-pixelformatdescriptor),
and [Skia image readback semantics](https://api.skia.org/classSkImage.html).
