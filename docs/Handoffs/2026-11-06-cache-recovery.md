# Cache recovery — 2026-11-06

Baseline 552177c. Selected and completed a bounded R-005 reliability slice after
reviewing the September handoffs: corrupt PNGs no longer cause persistent null tile
results. Empty/invalid entries are removed under the publication lock and rendered
again; valid hits and atomic cache publication retain their contracts.

[Evidence, tooling limits and next-slice acceptance](../02_Investigation/Cache-Recovery-2026-11-06.md).
Three regressions failed before the fix; all 84 tests now pass. Full Release solution
build (four library targets) and NuGet pack pass. No assets, version bump, commit,
remote issue update or publication.

Next: reproduce provider/source collisions, then define an optional stable dataset
revision token for cache keys as a separate R-005 change. Eviction remains separate.
F-003 bidi/cross-tile layout and existing GPU parity/hosting limits remain open.
