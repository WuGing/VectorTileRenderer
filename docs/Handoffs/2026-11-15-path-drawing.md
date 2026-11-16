# Path/drawing checkpoint — 2026-11-15

Baseline f79f882 plus pending performance work.
[Evidence and limits](../02_Investigation/Path-Drawing-2026-11-15.md).

SkiaCanvas batches point submission via AddPoly(open), using a bounded stack buffer
or returned pooled buffer. Paint/dash state is reused by value per canvas, with
brush edits checked each draw and owned paints disposed at drawing teardown.
Polygon paths now have deterministic draw-scoped disposal. Public inputs can change
between draws; clipping, vertices, fill and caller bitmap ownership are covered.

Path construction medians 31–52% lower in two captures, managed allocation 41% less.
Combined geometry-stage medians 13–20% lower. Complete fresh direct-PNG median
8–11% lower, warm 3–12% lower in main/repeat pairs. Warm managed allocation
15.044 → 13.071 MiB, fresh 33.084 → 31.111 MiB. Later confirmation slowed every
mode, including hits; all samples retained. Host variance limits latency claims.

173 Release tests, 15 Debug path tests, full restore-enabled Release solution build/
all targets/pack pass. Eight baseline/after PNGs byte-identical; all 2,400 measured
requests pass pixels/publication checks. Polygon disposal IL guard fails against
saved baseline and passes current code; Skia's registry skips path wrappers.
GPU runtime/performance and native heap totals remain separate validation scope.

Next review/checkpoint and profile the current complete request before another
filter/preparation or rasterization optimization. Work remains uncommitted;
no version bump or remote publication.
