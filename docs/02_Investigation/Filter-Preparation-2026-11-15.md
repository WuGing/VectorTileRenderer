# Repeated filters and layer preparation — 2026-11-15

## Result

Alternating complete direct-PNG requests against a saved baseline show heavy
fresh misses 8–12% faster and reused-provider misses 6–8% faster. Isolated
preparation medians fall 41–43%. Managed allocation falls approximately 2.52 MiB
per heavy request. All eight reference images and complete-request pixels match.

Baseline is `f79f882` plus the pending performance work through
[the post-style profile](Post-Style-Profile-2026-11-15.md). Saved baseline library
SHA-256 is `9F35C9E5DC6E93618F839A64F0CC4B021000CAF24C4C1CDD194CB38128EECA6A`.
All work remains uncommitted.

## Implementation

[Style.ValidateUsingFilter](../../VectorTileRenderer/Style.cs) now dispatches all
operations through one string switch. Recursive all/any/none and comparison/
membership bodies retain evaluation order, typed equality, current-culture string
comparisons, missing-attribute results and exception behavior. GetValue still
resolves dynamic tokens on each evaluation. No prepared filter snapshot is retained.

[Renderer.Render](../../VectorTileRenderer/Renderer.cs) now distinguishes feature
dependencies in paint/layout from the filter's need for attributes. For filtered
layers with feature-independent styling, the brush is created lazily when the
first feature passes, then reused within that layer/request. Rejected layers never
evaluate their paint. Feature-dependent styles continue to receive each feature's
attributes. Text placeholders introduced through `$id` also force per-feature
styling. Candidate enumeration keeps its concrete List type.

Dependency classification is recomputed for each layer/request. A focused test
reproduces a stale classification defect in the saved baseline after changing a
previously static layer to a feature-dependent style and filter. The baseline
throws KeyNotFoundException for the newly referenced attribute. The candidate
renders the newly selected feature correctly. This is a reproduced runtime defect.

Existing attribute and localized-geometry caches remain request-local. New brush
reuse is also request-local and respects changes between requests to filters,
paint/layout, layer IDs, zoom and scale. Concurrent mutation during rendering is
outside this experiment's validation. Public mutable input types remain in use.

GitNexus reports the committed index current at HEAD. Upstream impact reports
ValidateUsingFilter CRITICAL, 23 results / 17 processes / five modules, and
NeedsFeatureAttributes CRITICAL, 33 results / 18 processes / five modules.
Source confirms shared renderer use across demos and backends. Dirty additions
are outside the indexed snapshot. Validation covers portable semantics, all
Windows projects, library targets and exact CPU pixels.

## Measurement policy

Windows 11 Pro 26200, i7-13700K, 24 logical processors, approximately 31.3 GiB RAM,
SDK 10.0.400, .NET 10.0.11 x64, Skia assembly 3.119.0.0 CPU. Checked-in Zurich
MBTiles, repository styles/fonts, native TMS z14/8580/10646, Bright style, 512px.
OS filesystem caches are warmed/uncontrolled. No forced GC. Measurements run
sequentially, with builds/tests outside timing. UTC artifact dates are November 16,
local date November 15.

Separate-process request runs showed substantial host variation. For example,
the repeat candidate's hit median rose from 2.39 to 3.22 ms alongside rendering
slowdowns. Those runs are retained as exploratory evidence. The main comparison
uses [PairedRequestComparison](../../VectorTileRenderer.Benchmarks/PairedRequestComparison.cs)
to load both assemblies in one process, rotate fresh/warm/hit modes, and alternate
implementation order every round. Each implementation owns its style, provider
and cache. Five warmups/mode, 60 samples/mode/implementation, two runs.

Observers are disabled. Each sample includes canvas/provider setup, reflected
invocation, the complete request, encoding/publication and disposal. Exact decoded
pixels are checked outside timers after every request, and all published PNGs are
checked afterward. Reflection overhead is present in both implementations. Managed
counters exclude native/retained memory. Validation may influence later GC, and
alternation reduces host drift without eliminating it.

## Complete-request comparison

| Run/mode | Baseline median ms | Candidate median ms | Lower elapsed | Baseline p95 ms | Candidate p95 ms |
|---|---:|---:|---:|---:|---:|
| First fresh miss | 87.25 | 79.97 | 8.3% | 110.58 | 98.79 |
| Repeat fresh miss | 89.67 | 79.32 | 11.5% | 100.44 | 98.65 |
| First reused-provider miss | 56.33 | 53.05 | 5.8% | 71.12 | 68.08 |
| Repeat reused-provider miss | 56.88 | 52.47 | 7.8% | 69.99 | 60.69 |

Cache-hit medians are 2.52 → 2.46 ms and 2.46 → 2.43 ms. Hit p95 is 3.02 →
3.15 ms and 3.02 → 3.01 ms. No cache-hit improvement is attributed to this change.

Reused-provider mean managed allocation falls from approximately 12.31 MB to
9.66 MB/request, about 21.5%. Fresh falls from approximately 31.23 MB to 28.58 MB,
about 8.5%. The reduction is approximately 2.65 million bytes, or 2.52 MiB.

## Isolated attribution

The [preparation timer](../../VectorTileRenderer.Benchmarks/AllocationInvestigation.cs)
reuses the decoded tile and the existing no-op canvas, disables observers and
warms for at least one second. It times preparation, ordering and no-op dispatch,
returning a 1x1 bitmap. Real drawing, PNG encoding and fresh decoding are excluded.
The same compiled harness runs with either library, verified by recorded hashes.
100 requests/process, baseline→candidate then candidate→baseline ordering.

| Pair | Baseline median ms | Candidate median ms | Lower elapsed |
|---|---:|---:|---:|
| First | 24.77 | 14.19 | 42.7% |
| Reverse order | 22.97 | 13.58 | 40.9% |

Managed preparation bytes fall from approximately 10.02 MB to 7.37 MB, about 26.4%.
These attribution results accompany real-rendering pixel and complete-request checks.

The warmed-up synthetic filter comparison uses 100,000 calls/sample, 50 samples
per implementation. Numeric filters measure 2.68 → 1.70 ms, recursive filters
10.22 → 8.04 ms. The earlier run measures 2.72 → 1.81 and 10.86 → 8.91 ms.
Both filter workloads allocate zero in both implementations. Scalar/stops controls
vary with JIT/host conditions and supply no separate GetValue improvement claim.

## Correctness and validation

The eight-case CPU matrix covers Zurich z12/z14, Basic/Bright and 256/512px,
six rotating modes, 20 samples/mode. All 960 timed results pass exact pixels and
publication checks. All eight reference PNG SHA-256 hashes match the post-style
baseline. The two paired runs add 720 pixel-validated complete requests.

Four focused [renderer regressions](../../VectorTileRenderer.Tests/RendererTests.cs)
cover brush reuse/ownership across layers and requests, changed filters and paint
dependencies, lazy invalid-paint errors, and feature placeholders derived from the
layer ID. Two detect failures in the saved baseline, while the two compatibility
checks pass there. All four pass the candidate. Existing numeric/culture/membership/
recursive-filter tests pass with the consolidated dispatch.

- Restore-enabled Release solution build passes, zero warnings/errors.
- All 190 Release tests pass.
- All-target library packaging passes for netstandard2.0, net7.0, net8.0 and net10.0.
- Real-rendering matrix and paired request pixels/publications pass exactly.

CPU results cover the shared evaluator and renderer preparation. Actual GPU,
concurrent requests and UI scheduling remain separate validation workloads.

## Reproduction and local evidence

See [benchmark instructions](../../VectorTileRenderer.Benchmarks/README.md).

```powershell
dotnet build VectorTileRenderer.sln -c Release
dotnet test VectorTileRenderer.sln -c Release --no-build --no-restore
dotnet pack VectorTileRenderer/VectorTileRenderer.csproj -c Release --no-build --no-restore -o artifacts/preparation-package
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --paired-request-comparison . artifacts/preparation-before-bin/WuGing.VectorTileRenderer.dll artifacts/preparation-paired.json 60
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --preparation-timing . artifacts/preparation-isolated-after.json 100
```

- `artifacts/preparation-before-bin/` contains the saved pre-change output.
- [Paired results](../../artifacts/preparation-paired.json),
  [repeat](../../artifacts/preparation-paired-repeat.json), and
  [summary CSV](../../artifacts/preparation-paired-summary.csv).
- `artifacts/preparation-isolated-{before,after}.json` and matching `-repeat.json`.
- [Final filter comparison](../../artifacts/preparation-final-filters.json).
- [Eight-case matrix](../../artifacts/preparation-final-matrix/20261116-043202-9046c211/results.json)
  and [reference hash checks](../../artifacts/preparation-pixel-equality.json).
- Separate-process evidence under `artifacts/preparation-before`,
  `preparation-after`, `preparation-final-after`, `preparation-before-repeat` and
  `preparation-final-after-repeat`. Early broader candidate results are exploratory.
- Baseline/candidate regression TRX files under `artifacts/preparation-baseline-tests/results/`
  and `artifacts/preparation-candidate-tests/results/`.

Next profile complete requests on the candidate before selecting another slice.
Drawing and fresh decoding remain substantial in the prior profile. See
[handoff](../Handoffs/2026-11-15-filter-preparation.md).
