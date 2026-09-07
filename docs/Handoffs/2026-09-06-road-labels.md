# Road-label fix handoff — 2026-09-06

User priority: garbled production road labels in Mapsui and a consuming project.

Implemented locally against 9abe693: measured fit, centered rigid glyphs,
continuous clipped runs, accepted-label collision bounds and versioned PNG cache.
Details and reproduction commands: [investigation](../02_Investigation/Road-Label-Corruption-2026-09-06.md).

Validation: clean Release solution build, 53/53 tests, pack success, 108 fresh
Zurich tiles across CPU/verified GPU, two styles and three zoom levels. No commit,
remote issue publication or package publication performed.

Next: user validation after rebuilding/restarting Mapsui and updating the consuming
project reference. Remaining F-003 work includes Unicode shaping/fallback and
cross-tile placement/duplicates. Do not treat fewer labels on short roads as a
regression to partial-name rendering. GPU parity remains a separate open issue.
Direct2D work is paused; its prior experimental report is still unfinished.

## Bright follow-up

[Point-label edge cleanup](../02_Investigation/Label-Edges-2026-09-06.md) now checks actual multiline bounds, alignment, offsets and halos at every zoom. Cache version is 3. Full build and 63 tests pass; 108 fresh CPU/GPU tiles rendered. Whole-label omission at edges is intentional until coordinated placement exists. No road-straightening change.

## Bounded placement follow-up

[Road placement and performance](../02_Investigation/Road-Placement-2026-09-06.md): single-pass gentle-section selection, 15-degree per-turn and 35-degree accumulated limits. Cache version 4. Full build/66 tests pass. Colorado timings include mixed batch results and a focused phase rerun; no repeatable meaningful placement penalty found. Local screenshots cover reported Zurich streets through zoom 18.
