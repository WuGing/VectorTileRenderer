# Complete cache-request and finish timings — 2026-11-07

Inspected baseline `552177c374f19598a321581b785a35e1c66b94de` and cumulative local
cache work. Windows x64, .NET SDK 10.0.400.

## Source-confirmed gaps and contract

[Renderer](../../VectorTileRenderer/Renderer.cs) originally recorded textEnd after
FinishDrawing, so DrawTextMs conflated text and finalization/readback. Backend came
from CurrentBackendHint. RenderCached hits emitted no render callback, and a miss's
render callback preceded PNG encoding/publication. Existing Mapsui aggregation and
GPU validation consumers read those legacy fields, so their meanings are preserved.

Additive RenderProfile fields now expose DrawTextOnlyMs, FinishDrawingMs and
ActualBackend. Legacy DrawTextMs equals text-only plus finish time. ActualBackend
uses the completed SkiaGpuCanvas surface state (GPU or CPU fallback), SkiaCanvas
(CPU), otherwise Unknown. A hint does not establish GPU activation. Successful GPU
finish already requires readback; no new GPU-host experiment was run in this slice.

New [CacheRequestProfile](../../VectorTileRenderer/CacheRequestProfile.cs) and
Renderer.CacheProfileSink report one normal RenderCached completion, including hits
and null/missing output. CacheHit/HasBitmap distinguish outcomes; hits use DiskCache
and missing output Unknown. Phases are:

| Metric | Boundary |
|---|---|
| LookupMs | Key/setup/lookup locks, decode and corrupt-entry removal |
| RenderMs | Inner Render including its legacy observer and resource cleanup |
| EncodeAndPublishMs | Encode, write, publication lock, temporary cleanup, including unsuccessful optional attempts |
| TotalMs | Complete cached request through return preparation, excluding its own observer |

Hits have zero render/encode metrics. Total also includes gaps/overhead around phase
boundaries. A failed optional write can still produce a usable bitmap, so encode
duration does not certify cache persistence. Throwing requests do not emit completion
profiles. Observers are process-wide, captured at request entry, may run concurrently,
and cannot invalidate bitmap return by throwing. The disabled cache-profiling path
does not allocate a profile or add an outer async state machine.

Host queuing, external context creation, caller output encoding/disposal are outside
these API boundaries. Legacy RenderProfile.TotalMs still ends after FinishDrawing;
it excludes its callback and subsequent request-resource cleanup. No LRU, optimization,
performance regression bound or GPU parity acceptance change is introduced.

## Impact and validation

GitNexus reported baseline 552177c current; uncommitted helpers/new types are not
indexed. RenderProfile upstream impact includes Renderer, Mapsui, GPU harness and
desktop sample paths. Source verified their legacy field usage. Cumulative
detect-changes rated critical (77 flows), including previous cache changes and
adjacent unchanged helpers. Existing graph incompleteness is not treated as absence
of callers. Full builds and focused callback/ownership tests address that broad risk.

[RendererTests](../../VectorTileRenderer.Tests/RendererTests.cs) verify:

- CPU fallback despite GPU hint; finish/text sum boundaries and preserved legacy hint.
- One cache miss/hit report each, complete publication before observing completion,
  render/encode timings on miss, zero render/encode on hit, and exact returned pixels.
- Throwing observers preserve usable bitmaps; missing tiles report HasBitmap false.
- Finish failures propagate with neither successful render nor cache completion reports.

Validation: `dotnet test VectorTileRenderer.sln -c Release --no-restore --verbosity quiet`
passed **104/104, no skips**. Full Release solution/four-target build passed with
zero warnings/errors. NuGet pack to `artifacts/request-profiling-package` passed.
`git diff --check` and documentation links passed. No native pixel algorithm changed.

F-007 Mitigated: trustworthy additive fields are available, but existing consumers
must migrate before their output stops using hints/conflated text time. R-006 remains
In progress: instrumentation is not a measured bottleneck result. Next update one
benchmark/report consumer to collect these fields and measure controlled warm/fresh
provider, cache hit/miss, CPU/verified GPU complete requests with runtime/hardware,
allocations and median/p95. Retain existing GPU parity gates. No remote publication,
version bump or commit.
