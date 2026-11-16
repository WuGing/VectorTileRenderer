# Style-evaluation checkpoint — 2026-11-15

Baseline `f79f882` plus pending performance work. Numeric filter comparisons avoid
temporary double boxes, string comparisons retain current-culture semantics, and
GetValue uses one value-tuple array for eagerly converted stops.
[Evidence](../02_Investigation/Style-Evaluation-2026-11-15.md).

Warm heavy-request allocation falls approximately 706 KiB (5.3%), fresh 2.2%.
Warmed-up synthetic medians improve 39% numeric / 22% nested filters / 21% stops.
Complete direct-PNG fresh medians improve 1.6–2.7%, warm medians increase 1.0–2.8%,
and p95 tails increase. No consistent end-to-end latency win is established.

186 Release tests, restore-enabled solution build and all-target packaging pass.
The new numeric allocation guard fails the saved baseline and passes the candidate.
Exact CPU pixels pass across the eight-case workload and cache outputs, with all
eight reference PNG hashes identical to the pre-change post-path baseline.
Styles remain mutable, including nested filters, attribute references and stops.

Next profile the current complete request, then isolate per-feature attribute
preparation or repeated layer decisions if supported by the results. Preserve
mutable inputs when considering request-local reuse. Fresh decoding and native
drawing remain significant. Changes are uncommitted.
