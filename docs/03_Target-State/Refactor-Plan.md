# Proposed work

R-001's isolated validation experiment and readback/Auto fixes were authorized and implemented locally; host/thread integration and parity remain. R-002 lifetime contracts and R-003 fallback/shaping now have validated implementations; R-004 includes the fixed culture bug. Remaining subitems stay open. None has been remotely published as an issue.

## R-001: Validate and define GPU hosting

Status: In progress
Priority: High
Area: GPU / Hosting
Related findings: F-001, F-007, F-009
GitHub Issue:
Owner:
Target milestone:

Objective:
Prove real GPU rendering and establish a reliable fallback contract.

The isolated harness has completed its first runtime experiment. See [measured results](../02_Investigation/Gpu-Validation-2026-09-05.md) and the [validated readback/Auto fixes](../02_Investigation/Gpu-Failure-Fixes-2026-09-05.md). This status remains In progress because production hosting/thread affinity and the failed image gate are unresolved.

Implementation outline:
- Start with the matrix in Backend-Investigation.md. Bind context ownership to the host/render thread; detect surface and readback failure; report actual backend.
- [Parity diagnosis](../02_Investigation/Gpu-Parity-2026-09-05.md) isolates backend
  edge/blend differences. Evaluate primitive-aware acceptance and backend references;
  do not simply widen the original whole-image tolerance.

Risks:
- Context/thread affinity, device loss, changed failure behavior.

Validation:
- CPU fallback with no context; active GPU pixel parity; surface/readback failure; worker-thread transition; repeated render/shutdown; end-to-end timings.

## R-002: Define canvas and cache-writer lifetime

Status: Implemented
Priority: High
Area: Resource Ownership
Related findings: F-002
GitHub Issue:
Owner:
Target milestone:

Objective:
Make native cleanup deterministic without invalidating returned bitmaps.

Implementation outline:
- Specify owned/borrowed resources; add disposal/finally paths including early returns; give asynchronous encoding its own valid image lifetime.

Risks:
- Public API compatibility, double disposal, extra bitmap copies.

Validation:
- Immediate caller bitmap disposal, concurrent cache writes, early missing-tile exits, repeated render memory plateau and context shutdown.

## R-003: Complete and test text and symbol behavior

Status: Proposed
Priority: High
Area: Text / Symbols
Related findings: F-003, F-004
GitHub Issue:
Owner:
Target milestone:

Objective:
Render required point/icon primitives and preserve complete text.

Implementation outline:
- Create capability fixtures; replace glyph-count truncation with validated text handling; address optional/collision behavior and edge/path placement in small changes.

Risks:
- Font differences, collision ordering, image fixture brittleness.

Validation:
- Point/icon pixel checks; mixed scripts/combining marks/surrogates; adjacent tile labels, curves and halos; before/after demo images.

## R-004: Define style compatibility and close verified gaps

Status: Proposed
Priority: Medium
Area: Style Evaluation
Related findings: F-005
GitHub Issue:
Owner:
Target milestone:

Objective:
Publish an accurate supported-style matrix and implement prioritized gaps.

Implementation outline:
- Exercise operators and stop types; add color interpolation and actionable unsupported-input diagnostics where promised.

Risks:
- Behavior changes for existing styles and ambiguous expression semantics.

Validation:
- Synthetic expression/color tests and checked-in style render comparisons; distinguish invalid inputs from unsupported valid syntax.

## R-005: Bound and version disk caching

Status: Implemented
Priority: Medium
Area: Caching
Related findings: F-006
GitHub Issue:
Owner:
Target milestone:

Objective:
Prevent unbounded growth and stale imagery after source changes.

2026-11-07: [Explicit byte/age maintenance](../02_Investigation/Cache-Maintenance-2026-11-07.md)
implemented with filesystem/concurrency regressions. 100 tests and full build/pack
pass. Consumers must opt into revisions and maintenance; no automatic schedule/quota.

2026-11-06: [Corrupt PNG recovery](../02_Investigation/Cache-Recovery-2026-11-06.md)
and [opt-in source revision keys](../02_Investigation/Cache-Identity-2026-11-06.md)
implemented and validated with 92 tests, full build and pack. Original signatures
and unversioned keys preserved. Caller adoption and bounded eviction remain open.

Implementation outline:
- Define source revision and cache key policy; add bounded eviction and safe publication; preserve caller-controlled directories.

Risks:
- Evicting active data, file races, cache migration.

Validation:
- Two providers under one style, changed source revisions, corrupt entries, storage limits and concurrent readers/writers.

## R-006: Measure complete tile latency before optimization

Status: In progress
Priority: Medium
Area: Rendering, Caching
Related findings: F-007
GitHub Issue:
Owner:
Target milestone:

Objective:
Identify measured bottlenecks and make profiles trustworthy.

2026-11-15: [Fresh decode and hand-test build](../02_Investigation/Fresh-Decode-2026-11-15.md).
Direct geometry conversion improves complete fresh misses 9–10%, reducing managed
allocation by 2.36 MiB. Nonadvancing malformed ClosePath fails fast. 205 Release
tests, 20 Debug geometry/parser tests, build/pack/publish and exact pixels pass.
Current Mapsui build published for manual feedback. Next profile current requests
after that checkpoint. R-006 remains In progress.

2026-11-15: [Post-submission profile and cumulative progress](../02_Investigation/Post-Submission-Profile-2026-11-15.md).
Fresh decode now leads individual timers at 34%, observer-free drawing ~38% fresh /
50% warm. Path construction ~3–4%. Direct saved pre-parser/current comparison shows
42–43% fresh / 44–50% warm lower medians. 2,700 exact-pixel timings, two zero-loss
traces. Next isolate fresh decode for new-tile workloads, keeping drawing/filter
leads for reused tiles. R-006 remains In progress.

2026-11-15: [Line/polygon submission](../02_Investigation/Path-Submission-2026-11-15.md)
optimized with a canvas-owned scratch path and direct unclipped polygons.
Complete fresh misses 8–9% faster / warm 6–10%, replay ~16%, allocation lower
1.46 MiB/request. Full-path cache discarded after regressions. 196 Release tests,
21 Debug path tests, solution build/all-target pack/exact CPU pixels pass.
Next profile current requests. R-006 remains In progress.

2026-11-15: [Post-preparation complete profile](../02_Investigation/Post-Preparation-Profile-2026-11-15.md).
Drawing leads observer-free stack groups, ~42% fresh / 53% warm. Instrumented
preparation and fresh decode remain close. Two zero-loss traces, 1,260 validated
timings. Next isolate line/polygon submission and repeated path construction,
preserving clipping/order/mutable geometry and exact pixels. R-006 remains In progress.

2026-11-15: [Repeated filters and preparation](../02_Investigation/Filter-Preparation-2026-11-15.md)
optimized with consolidated dispatch and lazy per-layer/request brush reuse.
Alternating complete misses improve 8–12% fresh / 6–8% warm, managed allocation
falls 2.52 MiB, and isolated preparation 41–43%. Reproduced stale render dependency
classification fixed. 190 tests/restore-enabled build/pack/exact CPU pixels pass.
Next profile current requests. R-006 remains In progress.

2026-11-15: [Post-style complete profile](../02_Investigation/Post-Style-Profile-2026-11-15.md).
Preparation/style still leads, ~34% fresh / 48% warm, with decode and native drawing
substantial. Two zero-loss traces and 1,260 validated timings. Next isolate repeated
filter interpretation and feature/layer preparation before a request-local evaluator
experiment, preserving mutable inputs and evaluation semantics.

2026-11-15: [Value and filter evaluation](../02_Investigation/Style-Evaluation-2026-11-15.md)
optimized with typed comparisons and value-tuple stop buffers. Warm heavy-request
allocation falls 5.3%, fresh 2.2%. Complete latency remains mixed with higher p95
tails. 186 tests/restore-enabled solution build/all-target pack pass. Next profile
before isolating further attribute/layer preparation work. R-006 remains In progress.

2026-11-15: [Post-drawing complete profile](../02_Investigation/Post-Path-Profile-2026-11-15.md).
Preparation/style leads combined timers, ~32% fresh / 47% warm, with fresh decode
and drawing close. Value/filter stack entries remain prominent. Next isolate
filter/value and per-layer preparation work, preserving mutable style, type/zoom/
missing-attribute semantics and exact pixels. Native drawing/decoding remain leads.

2026-11-15: [Batched paths and per-canvas paint reuse](../02_Investigation/Path-Drawing-2026-11-15.md)
implemented, including deterministic polygon-path disposal. Geometry medians
13–20% lower; complete fresh medians 8–11% lower in focused pairs. Host variation
limits broad latency claims. 173 Release tests/15 Debug path tests/full build/pack
and exact CPU pixels pass. Next review/checkpoint, then profile current requests.

2026-11-15: [Post-PNG complete request profile](../02_Investigation/Post-Png-Profile-2026-11-15.md).
Drawing leads combined stages (~34% fresh / 47% warm), preparation/style ~29% / 41%,
PNG ~8% / 11%. Fresh fetch/decode remains ~29%. Next investigate geometry/path
construction versus native drawing with exact pixels and resource/clip checks.
Matrix timing variation limits absolute comparisons; two zero-loss traces agree
on the hot paths.

2026-11-15: [Measured lossless PNG settings](../02_Investigation/Png-Encoding-2026-11-15.md)
implemented for cache misses. Vector styles use unfiltered/zlib 1, raster-containing
styles use compact defaults. Eight map cases encode 73–81% faster, files 14–20%
smaller. Complete direct-PNG miss medians 23–24% lower warm / 15–16% lower fresh.
158 tests/full build/pack and exact pixels pass. Next review/checkpoint and profile
the updated request before more preparation/filter/path work.

2026-11-15: [Complete request profiling refreshed](../02_Investigation/Complete-Request-Profile-2026-11-15.md).
Heavy fresh mean 123 ms, PNG ~26%, decode ~24%, preparation/style ~23%, drawing ~26%.
Warm misses shift PNG to ~34% and preparation/style to ~37%. Two zero-loss stack
captures corroborate encoder/filter/path entry points. Next define a bounded PNG
encoder experiment with elapsed time, encoded size and exact pixel checks.

2026-11-15: [Direct raw-protobuf model conversion](../02_Investigation/Fused-Conversion-2026-11-15.md)
implemented in normal loading. Decode/conversion median 16–17% lower and allocation
14.5% lower; complete fresh direct-PNG median 10–11% lower across two runs, with
slightly higher p95. 148 tests/full build/pack and exact models/pixels pass. Next
review/checkpoint and profile the current complete request.

2026-11-15: [Private protobuf reader experiment](../02_Investigation/Protobuf-Buffering-2026-11-15.md)
stays isolated in benchmarks. Parser gain 3.5–8.1%, complete fresh requests slower,
allocation slightly higher. Standard in-memory input pilot yields no useful request
gain. 143 tests/full build/pack and exact outputs pass. Next define and measure
fused raw-model conversion using the standard wire reader.

2026-11-15: [Attribute parser source inspection and paired-tag adapter](../02_Investigation/Attribute-Parsing-2026-11-15.md)
reduce alternating parser median 36% and allocation 35.6%. Focused complete fresh
direct-PNG request medians improve 8–13% across main/repeat runs. 126 tests/full
build/pack, exact model hashes and pixels pass. Profile the current loader before
a separate protobuf buffering/materialization candidate.

2026-11-15: [Native parser/conversion split and owned-container initialization](../02_Investigation/Decode-Conversion-2026-11-15.md)
reduce fresh-decode allocation 7.6%, with unchanged exact models/pixels and roughly
flat request timing. 120 tests/full build/pack pass. Review/checkpoint then CPU-profile
before seeking another elapsed-time optimization.

2026-11-15: [Marked allocation traces and closure cleanup](../02_Investigation/Style-Allocations-2026-11-15.md)
cut warm managed allocations about 25% with unchanged pixels; no meaningful elapsed
gain claimed. 118 tests/full build/pack pass. Review/checkpoint before further
dictionary/Paint/geometry or decoder experiments.

2026-11-15: [Encoded-output experiment implemented and measured](../02_Investigation/Direct-Png-2026-11-15.md):
Mapsui directly returns PNG bytes, strict corruption validation retained; 116 tests,
full build/pack and 2,700 old/new comparison samples pass. Review/UI smoke and later
build/decode allocation investigation remain. R-006 remains In progress.

2026-11-15: [CPU workload matrix and repeat](../02_Investigation/Request-Workloads-2026-11-15.md)
now measured. Prioritize an encoded-output experiment preserving bitmap/cache
contracts; build/decode allocations and distinct-tile/concurrent/GPU workloads remain.

2026-11-07: [Additive request/finish metrics](../02_Investigation/Request-Profiling-2026-11-07.md)
implemented, 104 tests/full build/pack pass. Consumer migration and controlled
end-to-end workload measurements remain; no optimization claim.

Implementation outline:
- Separate actual backend, draw, finish/readback, encode/cache and request time. Compare cold/warm workload and concurrency. Extract Renderer blocks only with behavior-preserving coverage.

Risks:
- Instrumentation overhead, confusing cached with rendered requests.

Validation:
- BenchmarkDotNet reports with hardware/runtime, allocations and median/p95 end-to-end results; existing renderer regressions must pass.

## R-007: Evaluate one alternative renderer after baseline

Status: Proposed
Priority: Medium
Area: Rendering
Related findings: F-008
GitHub Issue:
Owner:
Target milestone:

Objective:
Determine whether another engine improves the chosen product workload.

Implementation outline:
- Resolve Q-002, then implement a small isolated spike from Backend-Investigation.md. Define proposed adapters and public API changes explicitly.

Risks:
- Native packaging, text parity, duplicated map-engine work, readback costs.

Validation:
- Same geometry/styles/fonts/output sizes, visual parity, startup and steady-state timings; build/pack target checks and native runtime deployment.


2026-09-07: [ownership, fallback/shaping and invariant parsing validation](../02_Investigation/Fork-Lessons-Implemented-2026-09-07.md). R-002 uses completed atomic publication rather than a background writer; full mixed-direction layout under R-003 remains open.
