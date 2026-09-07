# Street-label follow-up — 2026-09-05

The user prioritized upside-down street labels observed in another consuming
project. This was already reproduced under F-003 in the Colorado GPU experiment.

Implemented a bounded shared SkiaCanvas fix for path direction, bend calculation
and path length. [Evidence, commands and limitations](../02_Investigation/Street-Label-Orientation-2026-09-05.md).
Four direction regressions failed before and passed after; six new tests in total.
Full suite: 46 passed. Release solution build and packaging succeeded.
CPU/GPU Colorado 512px output now has upright WEST 31ST AVE. labels.
The pre-existing 256px GPU parity failures remain; both hardware runs exit 1.

Production changes and tests are uncommitted alongside the earlier GPU harness
and documentation. No GitHub issue was created or closed. Do not overwrite earlier
performance results with these five-iteration correctness reruns.

Next: review/deliver the label fix, then address R-001 Auto/readback errors.
Keep F-003 open for clipping/collision, multilingual truncation and looped paths.
