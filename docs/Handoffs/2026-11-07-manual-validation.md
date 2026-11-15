# Manual validation ready — 2026-11-07

Baseline 552177c plus cumulative uncommitted library/cache/profiling work. User asked
to stop at a concrete manual test checkpoint. [Manual checklist](../Manual-Validation.md).

Mapsui demo now uses source revision keys, dedicated validation cache, persisted
revision override, explicit trim/clear controls, and request/finish profiling. Old
hint-based aggregation was replaced with actual backend and cache-request reporting.
Provider retirement defers disposal until active requests finish. No library contracts
changed in this slice; disabled neighbor prefetch was removed from the demo.

Validation: 104 regressions pass; final full Release solution build passes with zero
warnings/errors. Live UI smoke: map visible, reload changed hits 351 → 383 while
misses stayed 2185; New revision changed revision 0 → 1 and misses 2185 → 2217.
Observed backend CPU, not a GPU activation claim. Last request after revision:
10.5 ms total, 0.4 lookup, 5.9 render, 4.1 encode/write; uncontrolled interactive
observation only, not a benchmark. Local screenshot under ignored artifacts at
manual-validation/ready.png is partly occluded by another window, so use the live
application for visual assessment. No baseline screenshot was captured.

GitNexus index baseline current; initial unqualified provider lookup resolved the
WinForms namesake. Exact Mapsui class identifier confirmed its source constructor
caller; source inspection verified MainWindow and request paths. Private/new symbols
remain incompletely indexed. Cumulative detect-changes reported critical/77 flows,
including prior renderer work. Full build plus focused UI and library tests address
scope; no implication of a clean graph for untracked symbols.

Remaining human checks: pan/zoom/style quality, clear/reload, trim status, restart
revision persistence and responsiveness. Existing F-003 bidi/cross-tile and GPU parity/
hosting limits remain. R-006 controlled performance measurements remain future work;
do not mistake rolling interactive diagnostics for benchmark results.

Final build was reopened successfully; UI confirmed persisted revision 1. The demo
is left open for the user's pass. Visual/cache reuse across restart remains manual.

No commit, version bump, issue update or package publication. Leave demo available
for the user's pass; do not start another feature slice before reviewing feedback.
