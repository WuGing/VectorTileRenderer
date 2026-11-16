# Post-submission profile and cumulative checkpoint — 2026-11-15

`f79f882` plus pending performance work.
[Evidence and reproduction](../02_Investigation/Post-Submission-Profile-2026-11-15.md).

2,700 exact-pixel timed requests, eight reference PNG hashes match. Two 150-request-
per-phase traces, zero lost events, all 622 publications match. Heavy instrumented
repeat medians fresh 72.34 ms, warm 50.18 ms, hit 2.32 ms.

Fresh decode leads individual timers at 34%, preparation/style 30%, drawing 26%,
PNG 9%. Warm preparation/style 48%, drawing 37%, PNG 14%. Observers add preparation
overhead. Observer-free drawing union ~38% fresh / 50% warm, filter/style union
~18% / 26–27%. Parser entry ~24% fresh, SetPathGeometry ~3–4%.

Fresh cumulative paired comparison against saved pre-parser build gives 121 ms →
70 ms (42–43% lower), warm 80–82 ms → 41–45 ms (44–50%). Managed allocation falls
40% fresh / 46% warm. This starting build already includes direct PNG output.
Recent value/filter, preparation and scratch-path slices together give 8–12% fresh /
19–26% warm lower medians. Direct comparisons, separate baselines and timing limits
are recorded in the report.

Next isolate fresh decode's protobuf/model/geometry costs for new-tile workloads.
Native drawing and filter evaluation remain leads for reused tiles. Path construction
has a smaller remaining budget. Keep complete-request/pixel validation and scope
actual GPU/concurrent/UI measurements separately.

Measurements/docs only. Benchmark build and runtime checks pass. Previous checkpoint
passed 196 Release tests, 21 Debug path tests, restore-enabled solution build and
all-target pack. Changes remain uncommitted, R-006 remains In progress.
