# Path-submission checkpoint — 2026-11-15

`f79f882` plus pending performance work.
[Evidence and limits](../02_Investigation/Path-Submission-2026-11-15.md).

Selected SkiaCanvas change reuses one owned scratch path and avoids the unclipped
polygon wrapper list. Current geometry is rebuilt every draw. Paths release on
FinishDrawing, restart and Dispose. Full geometry-path caching with mutation
snapshots was discarded after warm regressions and higher allocation.

Two alternating complete-request comparisons show fresh misses 8–9% faster, warm
6–10%, about 1.46 MiB less managed allocation per heavy request. Real-command
line/polygon replay improves ~16%. These CPU workload results retain host/timing
limits. Native allocation bytes/peak are unmeasured.

196 Release tests, 21 Debug path tests, restore-enabled solution build and all-target
packaging pass. All 960 matrix requests and 720 paired requests pass exact pixels
and publication checks. Eight reference hashes and all replay hashes match.

Next profile current complete requests before choosing further work. GPU, concurrent
and UI workloads remain separate. Changes are uncommitted, R-006 remains In progress.
