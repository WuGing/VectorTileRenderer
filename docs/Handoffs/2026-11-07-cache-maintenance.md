# Explicit cache maintenance — 2026-11-07

Baseline 552177c. Implemented TileCache.Prune: caller-invoked max-byte and optional
max-age cleanup for top-level rendered PNGs, oldest-written first, with result counts.
Excludes temp/unrelated/nested/link files and rejects linked directories. Shares the
renderer read/publication lock; per-file failures are counted. Future writes/external
processes can exceed the limit; this is not an automatic quota or LRU cache.

[API, evidence, safety boundaries and validation](../02_Investigation/Cache-Maintenance-2026-11-07.md).
100 tests passed, no skips; full Release solution/four targets and pack pass.
Includes actual link safety, locked-file failure and concurrent rendering tests.

R-005 Implemented; F-006 remains Mitigated pending consumer adoption. No demos are
assigned arbitrary revisions or maintenance schedules. No version bump, commit or
remote publication. Previous local changes remain in the working tree.

Next: R-006 profiling audit of complete request latency, including finish/readback,
encoding/publication and cache hits, before optimizing or choosing another backend.
F-003 bidi/cross-tile labels and GPU parity/hosting remain unresolved separate work.
