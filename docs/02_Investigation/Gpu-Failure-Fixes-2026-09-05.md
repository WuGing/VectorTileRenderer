# GPU readback and Auto recovery fixes

Inspected 2026-09-05 at `7bc45e1891402ab2fbda5a557ea94e3856fc9085` plus
the existing street-label working-tree fix. Related: [F-001](Findings/F-001.md),
[original experiment](Gpu-Validation-2026-09-05.md), R-001.
Changes remain local and uncommitted.

## Behavior

[`SkiaGpuCanvas.OnBeforeFinishDrawing`](../../VectorTileRenderer/SkiaGpuCanvas.cs)
now checks both the snapshot and ReadPixels result. Failure throws
InvalidOperationException with a GPU pixel readback message, instead of returning
an invalid bitmap. Repeating FinishDrawing on the failed surface still throws.
The existing GPU state is not relabeled CPU, which would bypass readback on the
next completion attempt. Start a new render to recover; failed surface creation
on an abandoned context still falls back to a fresh CPU render.

The canvas cannot replay already issued GPU commands onto a CPU surface. There
is therefore no automatic CPU retry after readback failure. Renderer.Render
propagates the exception. RenderCached propagates it on a cache miss before
scheduling the PNG writer; existing cache hits continue to bypass rendering.
Mapsui's provider already catches render errors and returns a missing tile.
Direct library consumers must handle the error and retry with a new CPU request
or valid hosted context as appropriate.

[`CanvasFactory.Create`](../../VectorTileRenderer/CanvasFactory.cs) no longer
stores process-global availability flags or a probe lock. Both Auto and Gpu
probe the current calling context on each creation. Cpu does not probe.
This permits recovery after an earlier no-context call, and prevents one
thread's availability result from deciding another thread's backend.
Repeated failed Auto calls now incur a probe; no performance claim is made for
per-request factory creation. Hosts can use Cpu explicitly when GPU is unwanted.

## Regression evidence

[Machine-readable results](Gpu-Failure-Fixes-2026-09-05-summary.json) retain before
and after checks and the full image-parity matrix. Raw local evidence is in
`artifacts/gpu-regressions-before/results.json` and
`artifacts/gpu-regressions-after/results.json`.

The Windows x64 harness used .NET 10.0.11 and NVIDIA RTX 4070 Ti, OpenGL 4.6,
driver 616.56, with a host-owned WGL context. GPU surfaces were created before
Skia context abandonment; this simulates Skia context loss, not physical removal.

| Check | Before | After |
|---|---|---|
| Failed readback throws, first and second completion attempt | Both fail | Both pass |
| New render after abandonment produces correct CPU pixels | Pass | Pass |
| Auto recovers after initial no-context failure and renders GPU pixels | Fail | Pass |
| Context-free worker renders CPU after hosted probe | Pass | Pass |
| Later hosted Auto still renders GPU pixels | Fail | Pass |

All seven hardware assertions pass, including the GPU-surface precondition.
Normal texture-backed output and independent checked readback also pass. The
existing synthetic geometry 256px image gate still fails (mean error 1.99329,
pixels over 32/255: 1.16119%); 11/12 image cases pass. Thus the overall hardware
run exits 1, not a blanket GPU pass. All 37 generated PNGs match the preceding
`artifacts/label-fix-zurich` run byte for byte. Colorado was not rerun for these
failure-path changes; its previous parity limitation remains open.

Two portable tests verify that completion failure propagates through Render and
RenderCached without a success profile or cache file. They simulate a completion
error; the hardware assertions above reproduce the actual GPU readback failure.

Commands:

```powershell
dotnet run --project VectorTileRenderer.GpuValidation -c Release -- . artifacts/gpu-regressions-after 5
dotnet build VectorTileRenderer.sln -c Release --no-restore
dotnet test VectorTileRenderer.sln -c Release --no-build --no-restore
dotnet pack VectorTileRenderer/VectorTileRenderer.csproj -c Release --no-build -o artifacts
```

Final solution build: zero warnings/errors across all four library targets and
Windows consumers. Tests: 48/48 passed. Package and symbol package generated.
An initial new-test assertion used an unsupported NUnit member; corrected before
the successful build/test run. Five-iteration hardware timings are validation
output, not a replacement for the earlier performance baseline.

## Scope and remaining work

GitNexus index matched HEAD; query reported missing FTS indexes. Context and
impact results missed source-confirmed factory consumers and virtual completion
calls, so source inspection established Renderer/RenderCached and Mapsui impact.
detect-changes reported high risk across the accumulated 17 tracked files and
eight flows, including prior label/docs work. Untracked harness/tests were also
reviewed directly. The validation above covers the changed failure contract.

F-001/R-001 remain open for production host/thread affinity and image parity.
Factory-created contexts and general canvas/native disposal still need R-002's
ownership work; the new factory checks exercise that existing lifetime path and
do not establish a native-memory plateau. IsGpuEnabled is not proof that every
future readback will succeed. No new public API or native context host is added.
