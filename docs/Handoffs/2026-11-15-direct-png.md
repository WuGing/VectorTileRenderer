# Direct PNG implemented and measured — 2026-11-15

Baseline 8e53adf plus prior uncommitted workload work. Added RenderCachedPng alongside
RenderCached and switched Mapsui to return bytes directly. Shared keys/publication,
strict codec-success validation on PNG hits, one encoding on misses, internal bitmap
disposal and caller-owned bytes. Full validation decode remains; no header-only bypass.
Cache profiles add HasOutput/OutputFormat; HasBitmap retains its original meaning.

[Contracts, measurements, raw links and limits](../02_Investigation/Direct-Png-2026-11-15.md).
116 tests pass; all library targets/full solution build and package pass. 2,400
alternating old/new CPU matrix samples plus 300 noisy-case repeat samples; exact
reference pixels and published files checked. Hit medians 87–91% lower; warm misses
9–27%, fresh misses 6–24%. Heavy Bright/z14/512 hit 31.30 → 2.80 ms; warm miss
118.48 → 89.61 ms. No universal p95 claim: one noisy miss tail was worse in the
first run, lower in the repeat. No GUI-frame/GPU or cold-filesystem result inferred.

Next review the new API/shared helpers, then a short Mapsui reload/revision smoke
pass. Build/style/decoded-provider allocation tracing can follow. No new renderer
pixel algorithm, dataset, GPU policy, package version, commit or remote publication.
Prior workload files remain uncommitted alongside this implementation.
