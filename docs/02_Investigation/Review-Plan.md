# Review plan — 2026-09-05

2026-11-15 checkpoint: [PNG/allocation work reviewed and validated](../Handoffs/2026-11-15-performance-checkpoint.md),
118 tests/full restore-enabled build/pack pass. Next isolate native decode/conversion
allocations before another optimization. Performance findings retain their measured
scope and GPU/text limitations remain open.

2026-11-15 allocation follow-up: [traced evaluator closure removed](Style-Allocations-2026-11-15.md),
warm managed allocations 18.85 → 14.08 MiB, essentially flat focused latency.
118 tests/full build/pack and eight byte-identical reference PNGs pass. See
[latest handoff](../Handoffs/2026-11-15-style-allocations.md).

2026-11-15 direct PNG follow-up: [API implemented and measured](Direct-Png-2026-11-15.md),
Mapsui migrated, 116 tests/full build/pack pass. Hit medians 87–91% lower in this
CPU matrix; no universal tail-latency claim. See the
[latest handoff](../Handoffs/2026-11-15-direct-png.md).

2026-11-15 workload follow-up: [CPU baseline measured](Request-Workloads-2026-11-15.md),
3,150 main/repeat samples, exact output checks. Encoding and provider reuse are
material; warm build/style allocations merit later profiling. Next define and test
encoded-output caching for Mapsui; see the
[latest handoff](../Handoffs/2026-11-15-workload-measurements.md). R-006 In progress.

2026-11-15: [Positive manual feedback](../00_User-Input/Manual-Validation-2026-11-15.md)
recorded. Cache/profiling/demo checkpoint reviewed with no blocking finding; see
[validation and next workload slice](../Handoffs/2026-11-15-reviewed-checkpoint.md).

2026-11-12: [Restore-time Source Link vulnerability blocker fixed](../Handoffs/2026-11-12-sourcelink-build.md).
SDK-bundled Source Link replaces obsolete package reference; restore-enabled builds
and 104 tests pass. Resume manual validation.

Scope: code comments and unfinished branches, README known issues, GPU correctness/performance assumptions, backend alternatives, existing docs, and GitHub tracking. Source baseline: `a4f8ca87dbb7870cbae9ca5d61881b0f3e0cedcc`. This is an investigation and documentation change, not a renderer fix.

## 2026-11-07 current follow-up

**Manual checkpoint:** Mapsui integration is ready. Follow the
[manual checklist](../Manual-Validation.md) and
[current handoff](../Handoffs/2026-11-07-manual-validation.md). Build/tests and live
reload/revision smoke checks pass. Review human feedback before further feature work.

[Request/finish profiling](Request-Profiling-2026-11-07.md) implemented with additive
fields/callbacks preserving legacy semantics. 104 tests, full build and pack pass.
F-007 Mitigated; R-006 In progress. Next migrate a benchmark/report consumer and
measure complete warm/fresh/cache hit/miss workloads before optimizing. See the
[latest handoff](../Handoffs/2026-11-07-request-profiling.md).

## 2026-11-07 cache maintenance

[Explicit bounded maintenance](Cache-Maintenance-2026-11-07.md) implemented:
100 tests pass, full Release build and pack pass. R-005 Implemented; F-006 remains
Mitigated pending caller adoption. Next R-006: audit complete request profiling
boundaries before changing metrics or optimizing. See the
[current handoff](../Handoffs/2026-11-07-cache-maintenance.md).

## 2026-11-06 cache follow-up

[Source revision keys](Cache-Identity-2026-11-06.md) now implemented as an opt-in
overload preserving existing signatures/keys: 92 tests, full Release build and pack
pass. F-006 Mitigated; R-005 In progress. Next define explicit caller-invoked cache
maintenance and filesystem safety cases before implementing eviction. Current
[handoff](../Handoffs/2026-11-06-cache-identity.md).

[Corrupt cache recovery](Cache-Recovery-2026-11-06.md) implemented and validated:
84 tests pass, full Release solution build and pack pass. R-005 source identity and
eviction remain open. Next bounded slice: reproduce same-style/provider cache
collisions and define a stable dataset revision token with compatibility and cache-hit
regressions. See the [latest handoff](../Handoffs/2026-11-06-cache-recovery.md).

Follow-up: the [GPU runtime experiment](Gpu-Validation-2026-09-05.md) is now implemented and executed against library baseline 7bc45e1. The original audit limits below describe the initial pass; consult the experiment for current runtime evidence.

The [249 MiB Colorado follow-up](Gpu-Colorado-2026-09-05.md) adds native MBTiles
provider measurements: eight of nine distinct tile/size parity cases passed,
no eligible case reached 1.10x total speedup, and provider-cache reuse mattered
substantially. Shared upside-down labels were also observed.

## Results and priority

| Priority | Record | Result / next work |
|---|---|---|
| High | [F-001](Findings/F-001.md) | Hosted GPU proven; Auto/readback fixed locally. Production host/thread integration remains. |
| High | [F-009](Findings/F-009.md) | 256px differences isolated to backend rasterization/blending; original parity gates still fail |
| High | [F-002](Findings/F-002.md) | Native resource lifecycle and background cache bitmap ownership need explicit contracts |
| High | [F-003](Findings/F-003.md) | Text fallback truncation and incomplete symbol/path placement |
| Medium | [F-004](Findings/F-004.md) | Point/icon drawing is a stub |
| Medium | [F-005](Findings/F-005.md) | Color interpolation and unsupported style cases |
| Medium | [F-006](Findings/F-006.md) | Disk cache has no eviction and does not identify source data |
| Medium | [F-007](Findings/F-007.md) | Profiling cannot isolate GPU finish/readback and uses a caller hint |
| Medium | [F-008](Findings/F-008.md) | Backend abstraction returns SKBitmap |
| Medium | Existing fixture task | Build visually meaningful adjoining-region fixture with documented provenance |

See [Evidence](Evidence.md) for all TODO markers and related comments, [Hypotheses](Hypotheses.md) for unmeasured performance risks, [Open Questions](Open-Questions.md), [Refactor Plan](../03_Target-State/Refactor-Plan.md), and [backend comparison](../03_Target-State/Backend-Investigation.md).

## Method and limits

Searched authored C# across the library, demos, tests and benchmarks for TODO/FIXME/HACK/XXX, NotImplementedException, question/performance/bug comments, and read surrounding implementations. Also reviewed README, existing docs and upstream issue bodies. Generated designer files and vendored/binary assets are not actionable TODO records.

GitNexus was stale at 00bb07a; refreshed it to the current checkout. Its context lookup found SkiaGpuCanvas, but query reported missing FTS indexes and returned no flows. Source inspection is the basis for conclusions; graph absence is not evidence. The graph even listed inherited methods as overrides, so source remains the authority.

No GPU host was launched, no pixel parity experiment or benchmark was run, and no historical upstream reproduction was replayed. Findings describe source-confirmed behavior; runtime impact still needs the stated validation. No production code or binary assets changed.

## Next sequence

Street-label priority follow-up: [orientation fix validated locally](Street-Label-Orientation-2026-09-05.md),
with six raster regressions and Colorado CPU/GPU visual evidence. F-003 remains
open for the broader text-placement issues. See the [latest handoff](../Handoffs/2026-09-05-street-labels.md).

1. R-001: [Auto/readback fixes validated locally](Gpu-Failure-Fixes-2026-09-05.md); [parity diagnosis completed](Gpu-Parity-2026-09-05.md). Evaluate primitive-aware acceptance before production GPU integration. See the [latest handoff](../Handoffs/2026-09-05-gpu-parity.md).
2. R-002: define native resource and cache-writer ownership with regression cases.
3. R-003/R-004: validate text/symbol and style gaps with small synthetic fixtures.
4. R-005/R-006: measure and improve cache policy and end-to-end instrumentation.
5. Only then choose a backend spike using R-007 and Q-002.


## 2026-09-06 priority update

[Production road-label corruption fixed locally](Road-Label-Corruption-2026-09-06.md):
53 tests pass, fresh CPU/GPU Zurich grids validated. See the
[handoff](../Handoffs/2026-09-06-road-labels.md) for rollout and remaining text work.
The alternative-backend experiment is paused.

Bright follow-up: [point-label edge cleanup](Label-Edges-2026-09-06.md) validated with 63 tests and 108 fresh tiles. Whole labels are omitted when they cannot fit; coordinated cross-tile placement remains future work.

[Bounded road-label placement](Road-Placement-2026-09-06.md) validated locally: 66 tests, close-up CPU/GPU Zurich grids and Colorado timings. Next validate interactively; cross-tile placement and Unicode remain.

[Preview.6 packaging handoff](../Handoffs/2026-09-06-preview6-release.md): updated version/release notes and fixed NuGet release-note encoding in CI. Package metadata verified locally; publication pending.

## 2026-09-07 external fork sanity check

[ststeiger fork evaluation](Ststeiger-Fork-2026-09-07.md): useful font-coverage and
ownership lessons, no ready-made road/GPU solution. New [F-010](Findings/F-010.md)
reproduces locale-dependent color parsing; prioritize its small fix before F-002
ownership and F-003 lossless fallback/shaping. See the
[handoff](../Handoffs/2026-09-07-ststeiger-review.md).

## 2026-09-07 implementation follow-up

[Fork lessons implemented](Fork-Lessons-Implemented-2026-09-07.md): F-010 closed,
F-002 mitigated, F-003 improved but still open. 81 tests pass; GPU ownership/failure
checks pass, original geometry parity failure remains. See the
[handoff](../Handoffs/2026-09-07-fork-lessons-implemented.md).
