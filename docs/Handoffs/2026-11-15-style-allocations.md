# Style allocation cleanup — 2026-11-15

Continued from direct PNG measurements. Added marked allocation workload/report
commands to the benchmark tool and traced Bright/z14/8580/10646 at 512 pixels.
[Evidence, commands, limits and raw-data links](../02_Investigation/Style-Allocations-2026-11-15.md).

Resolved sampled stacks exposed a captured Style.GetValue closure on scalar/filter
calls. Replaced the capturing array Select with an indexed recursive loop. A focused
regression reproduced 240 KB per 10,000 scalar evaluations before the fix and passes
the <4 KB budget afterward. Array inputs/attributes remain unchanged.

100-request marked windows, before → after managed MiB/request:
- Warm render 18.85 → 14.08 (25.3% lower).
- Build without drawing 15.01 → 10.25 (31.8% lower).
- Fresh decode 30.58 → 30.58 (unchanged).

Valid traces have zero lost events and no post-change GetValue captured-class samples.
Untraced warm median 55.35 → 54.69 ms: no meaningful latency-speedup claim. All eight
Basic/Bright z12/z14 256/512 reference PNGs match the older baseline byte-for-byte.
118 tests, full Release build/four targets and package pass.

Next review/checkpoint accumulated work, with a short Mapsui smoke pass if desired.
Remaining allocation leads: attribute dictionaries, Paint, localized geometry and
Mapbox parser data structures. Do not alter those contracts based on samples alone.
No native-memory, GPU or leak certification. No new interactive UI test in this slice.
Previous PNG/workload changes remain uncommitted with this implementation.
