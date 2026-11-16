# Filter/preparation checkpoint — 2026-11-15

`f79f882` plus pending performance changes.
[Evidence and reproduction](../02_Investigation/Filter-Preparation-2026-11-15.md).

Filter interpretation uses one operation switch. Filtered layers lazily reuse a
feature-independent brush within the current layer/request. Feature dependency
classification is recomputed per request, fixing a reproduced stale-classification
failure after changing static paint/filter inputs. Layer-ID text placeholders
remain feature-dependent. Candidate enumeration retains its concrete List type.

Two alternating same-process CPU comparisons show fresh complete direct-PNG misses
8–12% faster, warm misses 6–8% faster, approximately 2.52 MiB fewer managed bytes
per heavy request. Isolated preparation medians fall 41–43%. Separate-process
runs vary substantially, including cache-hit controls. Full limits are in the report.

190 tests, restore-enabled Release solution build and all-target packaging pass.
Four new regressions pass, with two detecting baseline failures. The eight-case
matrix's 960 requests and paired comparisons' 720 requests pass exact pixels and
publication checks. Eight reference hashes match the pre-change post-style baseline.

Next profile complete requests again before selecting another change. Drawing and
fresh decoding remain significant leads. Actual GPU/concurrent/UI workloads are
separate. Work remains uncommitted, R-006 remains In progress.
