# Fork lessons implemented - 2026-09-07

Baseline 3a263ea. User authorized addressing useful fork lessons.
[Report, contracts, timings and limits](../02_Investigation/Fork-Lessons-Implemented-2026-09-07.md).

Implemented invariant color parsing; explicit canvas/bitmap/GPU ownership;
completed atomic cache writes; shared raster-stream cleanup; actual glyph coverage,
lossless fallback and HarfBuzz shaping for font/script runs. Demos adopt disposal.
Cache version 5. No public ICanvas signature changes or package version bump.

81 tests pass; complete solution/four targets build cleanly. CPU/GPU map and Unicode
panels validated. GPU ownership/failure checks 8/8 pass; existing geometry parity
failure remains. Colorado Render timings similar; completed cache misses add about
10 ms encode/write in the sampled workload. Artifacts ignored under artifacts/.

F-010 Closed; F-002 Mitigated; F-003 remains open for complete bidi and cross-tile
placement. Platform fonts/native shaping still require non-Windows validation.
No code imported from the fork, no commit, remote issue change or publication.
