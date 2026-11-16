# Attribute parsing improvement — 2026-11-15

Baseline f79f882 plus pending decode/profiling work. Exact dependency source showed
two temporary filtered tag lists and unsized decoded collections. Internal
`Sources/PbfParser` now uses paired indexing and known capacities, reusing the
dependency protobuf contracts and geometry decoder. MIT notice ships in package.
[Evidence and limits](../02_Investigation/Attribute-Parsing-2026-11-15.md).

300 alternating parser samples each, median 25.42 → 16.28 ms, managed allocation
20.306 → 13.085 MiB. Complete fresh direct-PNG Bright 512 miss 126.97 → 110.37 ms
main and 120.17 → 110.02 ms repeat. End-to-end results have process/host variation.
Four model fingerprints and reference PNG match exactly; measured outputs and
publications pass pixel checks. 126 tests/full restore-enabled Release build/all
four targets/pack pass. Third-party notice verified inside package.

Protobuf-net fills a temporary read buffer then adds to generated model lists.
Packed varints grow incrementally. No per-request capacity option identified on
this path. Next profile the current adapter and define a bounded protobuf candidate
if still material. Benchmark parser-only CPU/allocation windows explicitly use the
external parser; fresh-decode uses the current production loader. The alternating
`--parser-comparison` measures external versus current adapter directly.

Work is uncommitted, no version bump or remote publication. Existing GPU, text and
cross-tile roadmap remains open.
