# Proposed work

These are proposals, not approved implementation or remotely published issues. Sequence R-001/R-002 first; use evidence to prioritize the rest.

## R-001: Validate and define GPU hosting

Status: Proposed
Priority: High
Area: GPU / Hosting
Related findings: F-001, F-007
GitHub Issue:
Owner:
Target milestone:

Objective:
Prove real GPU rendering and establish a reliable fallback contract.

Implementation outline:
- Start with the matrix in Backend-Investigation.md. Bind context ownership to the host/render thread; detect surface and readback failure; report actual backend.

Risks:
- Context/thread affinity, device loss, changed failure behavior.

Validation:
- CPU fallback with no context; active GPU pixel parity; surface/readback failure; worker-thread transition; repeated render/shutdown; end-to-end timings.

## R-002: Define canvas and cache-writer lifetime

Status: Proposed
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

Status: Proposed
Priority: Medium
Area: Caching
Related findings: F-006
GitHub Issue:
Owner:
Target milestone:

Objective:
Prevent unbounded growth and stale imagery after source changes.

Implementation outline:
- Define source revision and cache key policy; add bounded eviction and safe publication; preserve caller-controlled directories.

Risks:
- Evicting active data, file races, cache migration.

Validation:
- Two providers under one style, changed source revisions, corrupt entries, storage limits and concurrent readers/writers.

## R-006: Measure complete tile latency before optimization

Status: Proposed
Priority: Medium
Area: Rendering, Caching
Related findings: F-007
GitHub Issue:
Owner:
Target milestone:

Objective:
Identify measured bottlenecks and make profiles trustworthy.

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

