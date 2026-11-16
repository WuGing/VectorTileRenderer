# Lossless PNG encoding checkpoint — 2026-11-15

Baseline f79f882 plus pending performance work.
[Evidence and limits](../02_Investigation/Png-Encoding-2026-11-15.md).

Both cache miss APIs now share EncodeCachePng. Vector-only styles use None row
filter/zlib 1, raster-containing styles use default all-filter/zlib 6. Gradient
diagnostics motivated that raster choice; unfiltered output can grow substantially
for gradients/alpha patterns. All decoded pixels match legacy results.

5,130 measured encodes across main/repeat/diagnostics. Eight map cases encode
73–81% faster and produce 14–20% smaller files. Heavy Bright 512 repeat 29.55 →
5.84 ms; 389,245 → 331,866 bytes. Complete direct-PNG misses improve 23–24% warm
and 15–16% fresh in two runs; complete encode/publication ~30 → 7.5–7.8 ms.
Main/repeat p95 varies in both directions.

158 tests/full restore-enabled Release solution build/all four targets/pack pass.
1,920 complete requests and all encoder results pass exact pixel checks. Eight
reference render PNGs match prior hashes. Format/alpha/color-space/ownership,
mixed-raster policy and old-cache compatibility tests pass. Existing entry bytes
are returned on hits, and new entries use the selected policy.

Next review/checkpoint, then profile updated complete requests before another
optimization. Work remains uncommitted; no version bump or remote publication.
