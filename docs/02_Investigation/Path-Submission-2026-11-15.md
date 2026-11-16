# Line/polygon submission and path lifetime — 2026-11-15

## Result

The selected change reuses one native scratch path per drawing session and avoids
the temporary polygon wrapper list when clipping is disabled. Heavy complete
direct-PNG misses measure 8–9% faster fresh and 6–10% faster with a reused tile.
Line/polygon replay measures approximately 16% faster. Managed allocation falls
approximately 1.46 MiB per heavy request. Exact CPU pixels match all eight cases.

A full request-local geometry-path cache was evaluated first. Mutation snapshots,
lookup metadata and a preparation pass increased allocation and warm latency.
That candidate was discarded. Every selected draw rebuilds from current geometry.

Baseline is `f79f882` plus pending work through
[the post-preparation profile](Post-Preparation-Profile-2026-11-15.md).
Saved baseline library SHA-256 is
`8BAD96EA1BB55CF20EFC13704CC34C9B249733E14417E72746EBFFBF61252845`.
The selected library hash is
`B942706ABA6FF1B32485AAA784187220FBBE9C1024A22EF1F734FE46911B436B`.
Work remains uncommitted.

## Submission census

[SubmissionCensus](../../VectorTileRenderer.Benchmarks/SubmissionCensus.cs) counts
renderer line/polygon commands by geometry-list identity during real CPU rendering.
Point-marker internals and text paths are excluded. Counts establish reuse
opportunities without supplying timings.

| Case, 512px | Line calls | Polygon calls | Unique lists | Repeated calls |
|---|---:|---:|---:|---:|
| Basic z12 | 4,944 | 135 | 4,941 | 138 |
| Basic z14 | 3,509 | 3,346 | 6,818 | 37 |
| Bright z12 | 9,547 | 244 | 5,055 | 4,736 |
| Bright z14 | 5,064 | 6,699 | 6,881 | 4,882 |

Heavy Bright has 11,763 submissions, 41.5% repeated, with 68,382 submitted points
and 37,951 unique-list points. Basic has little identity reuse. Repetition alone
does not establish the benefit of retaining native paths and mutation metadata.

## Selected implementation

[SkiaCanvas](../../VectorTileRenderer/SkiaCanvas.cs) owns a lazy scratch SKPath.
SetPathGeometry rewinds it, restores EvenOdd fill, converts the current points and
submits the same open AddPoly geometry. Existing stack/ArrayPool point buffering
is reused. Line/polygon draw order and per-canvas paint reuse are preserved.
Unclipped polygons submit directly without allocating a one-element list.

ReleaseDrawing disposes and clears the scratch path on FinishDrawing, StartDrawing
restart and Dispose, alongside existing paint/surface resources. FinishDrawing
still transfers its returned bitmap to the caller. Failed submissions keep the
path owned until cleanup. Every draw observes changed coordinates and list length,
including transitions from large/nonfinite to small geometry. Clipping continues
through the existing clipping functions.

This changes native path ownership from temporary per-draw disposal to explicit
canvas-session ownership. The prior IL-specific finally test is replaced by native
handle/lifecycle assertions covering actual release and reuse. No geometry cache
or renderer prepass is retained in the selected implementation.

GitNexus reports the committed index current at HEAD. Upstream GetPathFromGeometry
impact is CRITICAL, 43 results / 21 processes / five modules. Source confirms
shared CPU/GPU-derived canvas use across demos, tests and validation executables.
The committed index predates pending path/paint changes. Current source and runtime
checks establish behavior. All-target builds and CPU geometry/lifetime tests cover
the changed shared implementation. Actual GPU execution remains a separate workload.

## Workload and timing policy

Windows 11 Pro 26200, i7-13700K, 24 logical processors, approximately 31.3 GiB RAM,
SDK 10.0.400, .NET 10.0.11 x64, Skia assembly 3.119.0.0 CPU. Checked-in Zurich
MBTiles, Bright style and repository fonts, native TMS z14/8580/10646, 512px.
OS filesystem caches are warmed/uncontrolled. UTC artifacts are November 16,
local date November 15. Timings, builds and tests run sequentially. No forced GC.

The existing paired harness loads baseline and candidate into one process,
alternates implementation order each round and rotates fresh/warm/hit modes.
Two runs, 60 samples/mode/implementation and five warmups. Observers are disabled.
Timings include reflected invocation, setup, complete request, encoding/publication
and disposal. Each implementation owns its style/provider/cache. Exact pixels are
checked outside timers after every request, and for all cache publications.
Validation may affect subsequent GC. Host variation and reflection overhead limit
interpretation. The reported gains apply to these checked-in CPU workloads.

## Complete-request comparison

| Run/mode | Baseline median ms | Selected median ms | Lower elapsed | Baseline p95 ms | Selected p95 ms |
|---|---:|---:|---:|---:|---:|
| First fresh miss | 73.73 | 67.88 | 7.9% | 90.61 | 85.91 |
| Repeat fresh miss | 76.85 | 69.86 | 9.1% | 90.47 | 81.69 |
| First reused-provider miss | 41.36 | 38.83 | 6.1% | 61.50 | 58.42 |
| Repeat reused-provider miss | 41.55 | 37.58 | 9.6% | 58.26 | 55.37 |

Hit medians are 2.35 → 2.35 ms and 2.37 → 2.43 ms, with similar p95.
No hit improvement is attributed to this change.

Warm mean managed allocation falls approximately 9.66 → 8.13 MB/request (15.8%).
Fresh falls approximately 28.58 → 27.05 MB (5.4%). The reduction is about
1.53 million bytes or 1.46 MiB. Native allocation bytes/peak and retained memory
are outside these counters. The implementation owns one scratch path for these
submissions, with its storage reused until session cleanup.

## Isolated line/polygon replay

Replay records real renderer line/polygon commands and draws them in original
order on fresh 512px CPU canvases. It includes surface setup, path construction,
submission, FinishDrawing and canvas disposal. It excludes source decoding,
preparation, point/text drawing and PNG encoding. At least one second of warmup,
then 50 iterations/process. The same compiled harness runs with either library.
Order is baseline→selected, then selected→baseline.

| Pair | Baseline median ms | Selected median ms | Lower elapsed |
|---|---:|---:|---:|
| First | 13.53 | 11.35 | 16.1% |
| Reverse order | 13.56 | 11.36 | 16.2% |

Replay managed allocation falls approximately 1,537,000 → 6,700 bytes. Pixel hashes
match across all four replay processes, and every timed replay matches its reference.
The geometry-only hash is
`A22D9EA1341F4CE99F8B88F7E87FEAE582D17DF256A4D98B05736360617F82F1`.
Complete-request measurements supply the end-to-end result.

## Rejected full-path cache

The exploratory candidate counted geometry-list submissions, retained native paths
for repeated lists, and compared Point[] snapshots before reuse to observe mutation.
It used a scratch path for other submissions. Heavy warm medians changed
44.84 → 50.01 ms and 46.46 → 47.66 ms. Fresh results were mixed, 74.53 → 72.52 ms
and 74.06 → 74.34 ms. Managed allocation increased about 1.07 MiB/request.
The selected candidate removes its dictionary, snapshots and renderer prepass.

Exploratory artifacts are `artifacts/submission-paired.json`,
`submission-paired-repeat.json`, `submission-matrix/`, and the local ignored
`submission-cache-experiment.dll`, hash
`E9EA8684D918EB9EA60F0B8539BD3E1843D3DA1830B1664EDAABEFE85C47B6CD`.
They document the rejected trial separately from selected `submission-final-*` results.

## Correctness and validation

[CanvasPathTests](../../VectorTileRenderer.Tests/CanvasPathTests.cs) now check
scratch identity/release, interrupted/restarted drawing, failed empty submissions,
mutated coordinates/counts, clipping toggles, large pooled geometry and nonfinite
coordinates followed by small valid geometry. Existing fill/verb/point, alpha,
dash, cap, paint-mutation and bitmap-ownership tests pass.

The final matrix covers Zurich z12/z14, Basic/Bright, 256/512px, six modes and
20 samples/mode. All 960 timed results and publications match exactly. All eight
reference PNG hashes match the pre-change preparation checkpoint. Two paired runs
add 720 exact-pixel complete requests. Replay hashes also match across builds.

- Restore-enabled Release solution build passes with zero warnings/errors.
- All 196 Release tests pass.
- All 21 Debug CanvasPathTests pass.
- Library packaging passes for netstandard2.0, net7.0, net8.0 and net10.0.
- CPU output and native handle/lifetime checks pass. GPU/concurrency/UI workloads
  remain separately scoped.

## Reproduction and local evidence

See [benchmark commands](../../VectorTileRenderer.Benchmarks/README.md).

```powershell
dotnet build VectorTileRenderer.sln -c Release
dotnet test VectorTileRenderer.sln -c Release --no-build --no-restore
dotnet test VectorTileRenderer.Tests/VectorTileRenderer.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~CanvasPathTests
dotnet pack VectorTileRenderer/VectorTileRenderer.csproj -c Release --no-build --no-restore -o artifacts/submission-package
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --submission-census . artifacts/submission-census.json
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --submission-timing . artifacts/submission-replay-after.json 50
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --paired-request-comparison . artifacts/submission-before-bin/WuGing.VectorTileRenderer.dll artifacts/submission-final-paired.json 60
```

- [Census](../../artifacts/submission-census.json).
- [Paired requests](../../artifacts/submission-final-paired.json),
  [repeat](../../artifacts/submission-final-paired-repeat.json),
  [summary CSV](../../artifacts/submission-final-paired-summary.csv).
- `artifacts/submission-replay-{before,after}.json` and matching `-repeat.json`.
  Baseline replay uses the current harness with only its library replaced by
  `artifacts/submission-before-bin/WuGing.VectorTileRenderer.dll`.
- [Final eight-case matrix](../../artifacts/submission-final-matrix/20261116-045919-eedf79fb/results.json)
  and [reference hash checks](../../artifacts/submission-final-pixel-equality.json).

Next profile complete requests on the selected implementation before selecting
another slice. See [handoff](../Handoffs/2026-11-15-path-submission.md).
