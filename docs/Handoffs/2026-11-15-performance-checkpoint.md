# Reviewed performance checkpoint — 2026-11-15

Reviewed cumulative work from 8e53adf before the local checkpoint commit. Includes
repeatable CPU request workloads, direct PNG output/Mapsui migration, marked
allocation collection/reporting, and closure-free scalar style evaluation.

## Review and verification

Self-review found no blocking issue in public bitmap compatibility, shared key and
atomic publication helpers, PNG corruption validation, internal bitmap disposal,
caller-owned output arrays, callback semantics or evaluator array behavior. This is
not an independent review or a release approval.

- Restore-enabled Release solution/four library targets: zero warnings/errors.
- Regression suite: **118 passed, no skips**.
- Library/symbol packages generated locally under ignored artifacts/performance-checkpoint.
- Prior eight-case exact pixel/publication checks and byte-identical before/after
  reference PNGs remain evidence; no implementation changed after those checks.
- Documentation links and git diff checks passed. Raw diagnostic tools, traces,
  generated PNGs and measurement JSON/CSV remain ignored local artifacts.
- GitNexus baseline current at 8e53adf; new helpers remain unindexed. Cumulative
  detect-changes reports critical/82 flows and adjacent unchanged symbols. Earlier
  HIGH-impact source inspections, regression tests, full builds and pixel evidence
  cover the shared renderer/evaluator changes; graph omissions are not no-risk proof.

## Measured scope and remaining work

[Direct PNG comparison](../02_Investigation/Direct-Png-2026-11-15.md): median hits
87–91% lower across the measured CPU matrix; miss savings smaller. Validation
decoding retained. No general GUI-frame-rate or universal p95 claim.

[Style allocation evidence](../02_Investigation/Style-Allocations-2026-11-15.md):
warm managed allocation 18.85 → 14.08 MiB, while focused median latency was roughly
flat. No native-memory/retained-heap claim. Existing GPU parity/hosting, bidi and
cross-tile placement limits remain open. No additional interactive Mapsui smoke pass
was run for this review; automated pixel and contract checks do not replace it.

Next bounded investigation: fresh native decoding, which still allocates about
30.58 MiB per selected heavy tile. Use resolved stacks and source to distinguish
external parser work from library adaptation before changing data/ownership contracts.
Prefer a small verified conversion change over a parser replacement; require
per-feature/geometry equality and before/after complete request measurements.
Attribute dictionaries, Paint and localized geometry remain separate warm-build leads.

This checkpoint creates a local baseline only. No package version bump, remote push,
PR, issue update or publication.
