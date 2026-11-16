# Direct conversion checkpoint — 2026-11-15

Baseline f79f882 plus pending performance work.
[Evidence and measurements](../02_Investigation/Fused-Conversion-2026-11-15.md).

Normal `PbfTileSource.LoadStream` now selects `PbfParser.ParseRendererTile`.
The standard protobuf reader feeds raw features directly into renderer features,
avoiding decoded Mapbox objects, attribute lists and ID strings. Existing geometry
decoding is reused; normalization and geometry-type mapping are shared with the
staged reference adapter. All containers retain request-owned construction.

300/500 alternating pairs show decode/conversion medians 16.9%/16.5% lower,
managed allocation 20.755 → 17.747 MiB (14.5% less). Complete fresh direct-PNG
Bright 512 medians 120.83 → 107.43 ms main and 116.59 → 104.77 ms repeat
(10–11% lower), with slightly higher p95. Complete managed allocation 36.148 →
33.139 MiB. No tail-latency/native-memory/GPU claim.

148 tests/full restore-enabled Release build/all four targets/pack pass. Four model
hashes and four reference PNGs match; all 960 measured complete requests and cache
publications pass pixel checks. New `--fused-comparison` measures final-model output
from both paths. Existing isolated parser/conversion windows keep their documented
scope. Next review/checkpoint, then profile current complete requests before choosing
another optimization. Work is uncommitted; no version bump or remote publication.
