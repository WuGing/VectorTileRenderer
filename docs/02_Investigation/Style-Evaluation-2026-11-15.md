# Value and recursive filter evaluation — 2026-11-15

## Implementation and scope

Inspected baseline `f79f882` plus the pending parser, PNG, drawing and profiling
work. The saved pre-change Release output is `artifacts/style-eval-before-bin`.
This slice changes [Style.ValidateUsingFilter and GetValue](../../VectorTileRenderer/Style.cs).

- Compare numeric operands as unboxed doubles, avoiding two temporary boxes for
  each numeric filter comparison. Preserve Double.CompareTo behavior, including NaN.
- Compare string operands directly with the existing current-culture semantics.
- Replace zoom-stop LINQ projection, reference tuples and list with one array of
  value tuples. Convert every stop eagerly before reading zoom, retaining exception
  order and the `source` parameter on malformed stop collections.

Recursive all/any/none traversal retains its evaluation order and short-circuit
behavior. Membership retains typed object equality. Styles and attributes remain
mutable and are read on each evaluation. Endpoint and interpolation math are
preserved. The candidate introduces no persistent evaluator cache.

An initial literal-string early return was removed after exploratory isolated
measurements. Those early synthetic runs had short warmups. The final comparison
adds at least one second of alternating warmup per workload before ten additional
warmup rounds, reducing tiered-compilation sensitivity.

GitNexus reported the HEAD index current at `f79f882`. Upstream impact rated both
GetValue and ValidateUsingFilter CRITICAL, with 30/23 returned results across
18/17 processes and five modules respectively. Source confirms their shared use
through ValidateLayer/ParseStyle and renderer preparation, affecting every demo
and backend. Dirty additions are outside the indexed snapshot. Regression checks
and CPU pixel comparisons cover the changed shared evaluator.

## Measurement policy

Windows 11 Pro 26200, i7-13700K, 24 logical processors, approximately 31.3 GiB RAM,
SDK 10.0.400, .NET 10.0.11 x64, SkiaSharp 3.119.0.0 CPU backend. Checked-in Zurich
MBTiles, Basic/Bright styles and repository fonts. Native TMS z12/2145/2662 and
z14/8580/10646, 256/512 pixels. The heavy case has 16,241 features.

[StyleEvaluationComparison](../../VectorTileRenderer.Benchmarks/StyleEvaluationComparison.cs)
loads the saved baseline assembly separately and alternates static delegates on
identical synthetic inputs. Each sample performs 100,000 evaluations. Managed
counters exclude native allocations. Complete requests use the existing rotating
six-mode harness with five warmups, fresh canvases, reused styles, unique revisions
for misses, and observers enabled. Fresh providers use warmed filesystem caches.
Output validation runs outside request timers and may influence later GC.

All measurements run sequentially, without overlapping builds, tests or profilers.
No forced GC. This is a CPU experiment. Hardware/backend validation is separate.

## Complete-request results

The final warmed-up isolated comparison records these medians per 100,000 calls.

| Workload | Before ms | After ms | Lower elapsed | Before bytes | After bytes |
|---|---:|---:|---:|---:|---:|
| Numeric filter | 3.45 | 2.11 | 38.8% | 4,800,000 | 0 |
| Recursive filter | 12.60 | 9.80 | 22.2% | 9,600,000 | 0 |
| Three zoom stops | 9.38 | 7.43 | 20.8% | 24,800,096 | 9,600,096 |

The stop workload allocates 61.3% less, including its boxed interpolation result.
The repeat records numeric 5.92 → 3.66 ms, nested 23.07 → 16.82 ms and stops
15.80 → 11.54 ms, with identical allocation counts. Relative improvements across
the two runs are 38–39%, 22–27% and 21–27% respectively. Both implementations
slow substantially in the repeat, reinforcing the host-variation limit.
The literal-string control allocates zero in both modes. Its small timing varies
with warmup and JIT layout, so no literal-path optimization claim is made.

Heavy Bright z14, 512px, direct PNG. First pair has 50 requests per mode, repeat
30. Baseline first pair precedes implementation. The repeat baseline immediately
precedes the final candidate.

| Run | Warm median ms | Warm p95 ms | Fresh median ms | Fresh p95 ms |
|---|---:|---:|---:|---:|
| Before, 50 | 64.78 | 74.57 | 85.04 | 99.55 |
| After, 50 | 66.57 | 80.75 | 83.69 | 106.64 |
| Before, 30 | 63.69 | 76.14 | 85.01 | 105.55 |
| After, 30 | 64.33 | 81.33 | 82.72 | 113.53 |

Fresh medians are 1.6–2.7% lower, warm medians 1.0–2.8% higher, and p95 tails
increase. These runs establish no consistent complete-request latency gain.
Warm style-evaluation median falls from 12.35 to 9.94 ms in the first pair.
Other preparation, drawing and GC variability absorb that improvement.

Warm managed bytes fall from approximately 13,705,480 to 12,982,600 per request,
about 706 KiB or 5.3%. Fresh bytes fall from 32,622,080 to 31,899,136, about 2.2%.
Direct cache-hit allocation stays approximately 340 KB, and hit medians are
2.45–2.55 ms. Whole-request allocation is the stronger repeatable outcome.

Initial broader-candidate measurements are retained under `artifacts/style-eval-after`,
`style-eval-after-repeat` and `style-eval-matrix` for provenance. The final narrower
candidate is identified by the `style-eval-final-*` artifact names below.

## Correctness and validation

[StyleEvaluationTests](../../VectorTileRenderer.Tests/StyleEvaluationTests.cs)
adds 13 tests covering 1,350 numeric comparison pairs, three cultures, typed
membership, missing attributes, composite short-circuit/exception behavior,
mutable predicates and attribute arrays, stop interpolation/mutation and malformed
input evaluation order. The allocation regression fails against the saved baseline
with 480,000 bytes for 10,000 comparisons and passes the candidate.
The other 12 new compatibility tests pass against the saved baseline as well.

Restore-enabled Release solution build passes with zero warnings/errors. All 186
tests pass. Library packaging passes for all four target frameworks. Native test
coverage uses CPU rendering. All complete-request outputs and published PNGs are
decoded and checked exactly. Eight-case reference images are compared to the
pre-change post-path references by SHA-256.
The final matrix contains 480 timed requests. Including the final focused candidate
and baseline runs gives 1,440 timed requests used for final comparisons/validation.

## Reproduction and local evidence

```powershell
dotnet build VectorTileRenderer.sln -c Release
dotnet test VectorTileRenderer.sln -c Release --no-build --no-restore
dotnet pack VectorTileRenderer/VectorTileRenderer.csproj -c Release --no-build --no-restore -o artifacts/style-evaluation-package
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --style-evaluation-comparison artifacts/style-eval-before-bin/WuGing.VectorTileRenderer.dll artifacts/style-eval-steady-isolated.json 50
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --request-timing . artifacts/style-eval-final-after 50 bright-z14-8580-10646-512 png-comparison
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --request-timing . artifacts/style-eval-final-matrix 10 * png-comparison
```

Local ignored evidence includes

- `artifacts/style-eval-before/20261116-040716-c9c9ad53/results.json`
- `artifacts/style-eval-final-after/20261116-041605-7561b56b/results.json`
- `artifacts/style-eval-final-before-repeat/20261116-041631-74da7b9b/results.json`
- `artifacts/style-eval-final-after-repeat/20261116-041647-d8b96e4d/results.json`
- `artifacts/style-eval-final-matrix/20261116-041704-71d1b05f/results.json`
- `artifacts/style-eval-steady-isolated.json` and `style-eval-steady-isolated-repeat.json`
- `artifacts/style-eval-pixel-equality.json` for all eight reference hashes.
- `artifacts/style-eval-baseline-tests/results/` for the expected baseline failure.

## Next investigation

Profile the current complete request before selecting another change. Remaining
preparation work includes per-feature attribute construction and repeated layer
decisions. Preserve public mutable inputs when considering request-local reuse.
Fresh decode and native drawing remain substantial in the prior profile.
See [checkpoint](../Handoffs/2026-11-15-style-evaluation.md).
