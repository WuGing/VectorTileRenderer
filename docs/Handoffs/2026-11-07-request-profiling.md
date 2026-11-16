# Request profiling — 2026-11-07

Baseline 552177c plus cumulative uncommitted cache work. Added text-only, finish and
observed-backend fields without changing legacy RenderProfile semantics. New
CacheProfileSink reports full cache hits/misses/missing-output requests, split into
lookup, render, encoding/publication and total time. No profiles for thrown requests;
observer failures leave output usable.

[Contracts, evidence, limits and validation](../02_Investigation/Request-Profiling-2026-11-07.md).
104 tests passed, no skips; full Release solution/four targets and pack passed.
F-007 Mitigated pending consumer migration; R-006 In progress pending real workload
measurements. Next adapt a benchmark/report consumer and measure complete requests
before optimizing. No GPU runtime/visual or performance speedup claim.

No commit, version bump or remote publication. Prior cache work remains in the tree.
Bidi/cross-tile placement and GPU parity/hosting remain separate unresolved work.
