# Line/polygon drawing and path construction — 2026-11-15

Inspected `f79f882` plus pending [PNG/performance work](Post-Png-Profile-2026-11-15.md).
CPU path batching and per-canvas paint reuse are implemented, with exact render
reference pixels preserved in the exercised cases.

## Implementation and ownership

[SkiaCanvas.GetPathFromGeometry](../../VectorTileRenderer/SkiaCanvas.cs) casts points
to SKPoint in their original order and submits one open `AddPoly` call. EvenOdd
fill and explicit repeated closing points follow the legacy path data. Up to 128
points use at most 1 KiB of stack storage; larger shapes borrow an ArrayPool buffer
returned in finally. Native path construction copies the submitted span. Exceptions
dispose a partially created path and return its temporary buffer.

Line and polygon paints are canvas-owned. Their snapshots compare the actual cap,
float stroke width and byte color used by Skia. Dash values are copied as floats
and checked on every call, including in-place edits, pattern removal and replacement.
Negative/NaN stroke widths preserve the legacy fresh-paint hairline behavior.
Paired paint/dash resources are released during drawing teardown, canvas reuse and
disposal. Public drawing entry points support changing brush values between calls.
Paths have draw-scoped lifetimes, so caller geometry mutation requires no retained
path-cache invalidation policy.

Polygon paths now have explicit using/finally disposal scopes. The baseline relied
on finalization. A compiled-scope regression fails against the saved baseline and
passes with this implementation. SKPath opts out of SkiaSharp's object registry;
the final test inspects its compiled finally/Dispose scope. Paint handle cleanup is
also checked at runtime on FinishDrawing and interrupted drawing disposal.

## Measurement method

Windows 11 Pro 26200, i7-13700K, 24 logical processors, 31.3 GiB visible RAM,
SDK 10.0.400, .NET 10.0.11 x64, SkiaSharp 3.119.2. Release CPU rendering, sequential
requests, warm/uncontrolled file caches. Profilers, builds and tests ran separately
from latency measurements. Local date is November 15; raw artifact timestamps use UTC.

[PathConstructionComparison](../../VectorTileRenderer.Benchmarks/PathConstructionComparison.cs)
alternates legacy and batched builders on 6,947 nonempty line/polygon parts,
38,479 points, from native Zurich z14/8580/10646 localized to 512. Five warmups,
50 measured geometry-set traversals per mode. Both modes dispose every path inside
timers. Before timing, fill type, point arrays and verb counts are compared for
all shapes. This isolates construction/disposal from rendering and source access.

Complete Bright 512 measurements use the existing six-mode rotating harness, five
warmups, 50 samples/mode main and 30/mode repeat. Repeat process order is after/
before. Fresh creates a provider per request, including source setup/decode,
rendering, PNG publication and harness-covered disposal. Warm uses the loaded
provider/model. Output and publication checks happen outside timers.

## Results

| Isolated geometry-set traversal | Legacy | Batched |
| --- | --- | --- |
| Main median | 1.82 ms | 1.25 ms |
| Final-code confirmation median | 2.73 ms | 1.31 ms |
| Managed allocation | 0.901 MiB | 0.530 MiB |

Construction medians are 31–52% lower in these captures, managed allocation about
41% lower. The same single native submission replaces per-point native calls.
Absolute times vary with the host. Native allocation/retained-memory totals were
outside the counters.

A batching/disposal-only pilot showed warm complete median 68.27 → 63.90 ms,
fresh 91.55 → 94.31 ms. Paint/dash reuse was then added to address repeated native
paint construction and setter work in the same drawing methods.

| Complete direct-PNG miss | Main before → after | Repeat before → after |
| --- | --- | --- |
| Warm total median | 68.27 → 66.25 ms (3.0% lower) | 68.99 → 61.05 ms (11.5% lower) |
| Fresh total median | 91.55 → 84.51 ms (7.7% lower) | 98.56 → 88.15 ms (10.6% lower) |
| Warm geometry-stage median | 18.25 → 15.87 ms (13.0% lower) | 17.90 → 15.17 ms (15.2% lower) |
| Fresh geometry-stage median | 18.13 → 14.96 ms (17.5% lower) | 18.99 → 15.15 ms (20.3% lower) |

Warm managed allocation is 15.044 → 13.071 MiB/request (13.1% less), fresh
33.084 → 31.111 MiB (6.0% less). The final scope includes batching, paint reuse and
deterministic polygon path disposal; complete-request results include all three.

Tail behavior varies. Main warm p95 is 80.92 → 78.77 ms, fresh 119.72 → 101.30 ms;
repeat warm 98.28 → 96.15 ms, fresh 119.35 → 116.11 ms. A later final-code run
slowed all modes, warm/fresh/hit medians 126.58 / 200.30 / 3.98 ms. This includes
hits that bypass drawing. Its pixels still passed; host variation limits performance
inference from that confirmation. All samples are retained in the comparison CSV.
No universal latency or tail guarantee follows from this host/workload.

## Validation and scope

- 173 Release tests pass, plus all 15 CanvasPathTests in Debug. New cases cover
  tiny/large/pooled paths, repeated/closing vertices, exact path data, clipped
  line/polygon pixels, alpha/dash/cap changes, in-place dash edits, canvas reuse,
  invalid widths, nonfinite/overflowing coordinates, paint cleanup and polygon
  path disposal scope. The saved-baseline disposal guard fails as expected.
- Eight before/after reference PNGs are byte-identical to the pre-change profile.
  All 2,400 measured complete requests across baseline/pilot/main/repeat/matrix/
  final confirmation pass exact pixels and publication checks. Renderer observers
  are enabled for these request timings and stage counters.
- Restore-enabled full Windows Release solution build passes for all four library
  targets and demos. Pack succeeds in `artifacts/path-final-package`; third-party
  notice is present. The standalone SDK vstest baseline attempt timed out connecting
  to its test host. The project runner completed the expected baseline failure and
  the normal current suites successfully.
- GitNexus committed index matches HEAD. GetPathFromGeometry impact is CRITICAL,
  43 upstream results across drawing/text, CPU/GPU subclasses, demos and tests.
  SkiaCanvas impact is partial (25 direct results) with exact graph error
  `Write operations are not allowed. The pool adapter is read-only.` Source
  inspection confirms shared base-class use. Whole pending-diff scope reports
  critical aggregate risk, 13 tracked files / 59 symbols / 66 flows, including earlier
  work. New untracked tests/benchmarks are outside those graph counts.
- Timing/pixel evidence is CPU/Windows. Actual GPU surface/readback parity and
  GPU performance require their separate hardware experiment. Native bytes, leaks
  and retained heap were outside these measurements. Polygon cleanup is verified
  by the compiled ownership scope.

## Local evidence and next step

Reproduce with the [benchmark README](../../VectorTileRenderer.Benchmarks/README.md).

- [Path benchmark](../../artifacts/allocation-trace/path-construction.json) and
  [final-code confirmation](../../artifacts/allocation-trace/path-construction-final.json).
- [Complete-request comparison CSV](../../artifacts/allocation-trace/path-request-comparison.csv).
- Main baseline `artifacts/path-before/20261116-022520-5bad2e1f`, batching pilot
  `artifacts/path-after/20261116-023005-1b09d877`, combined result
  `artifacts/path-paint-after/20261116-023720-f3e67fc9`.
- Repeat after `artifacts/path-paint-after-repeat/20261116-023745-05d393ea`, before
  `artifacts/path-before-repeat/20261116-023801-524c9ceb`.
- Eight-case matrix `artifacts/path-matrix-after/20261116-023819-f9602cad` and late
  confirmation `artifacts/path-final-confirmation/20261116-025246-9ae28206`.
- Saved baseline binaries `artifacts/path-before-bin`; baseline regression TRX files
  under `artifacts/path-baseline-tests/results`.

Next review/checkpoint the accumulated changes and profile current complete
requests before selecting further filter/preparation or native rasterization work.
Work remains uncommitted; R-006 remains In progress.
