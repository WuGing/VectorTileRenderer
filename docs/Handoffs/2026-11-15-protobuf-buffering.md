# Protobuf experiment checkpoint — 2026-11-15

Baseline f79f882 plus pending attribute/decode work.
[Evidence and measurements](../02_Investigation/Protobuf-Buffering-2026-11-15.md).

Pooled protobuf read buffers were replaced experimentally with private direct-append
Tile/Layer/Feature serializers. Parser medians improve 3.5–8.1% across runs, with
0.8% more managed allocation. Complete fresh direct-PNG misses are 6.3–7.3% slower
in two focused runs. Standard in-memory input pilot improves complete median 1.1%
with 5.2% more managed allocation and noisy controls/tails. Both candidates stay
outside the normal loader. Production uses the attribute-optimized standard reader.

Direct-reader source is isolated under `VectorTileRenderer.Benchmarks/ProtobufExperiment`,
run with `--protobuf-comparison`. Tests link these files directly, keeping portable
wire coverage independent of BenchmarkDotNet. 143 tests, full restore-enabled
Release solution build/all four targets/pack pass. Exact model hashes and six
reference PNGs match; all 1,320 measured complete requests pass output checks.

Next define a fused raw-protobuf-to-renderer adapter and compare its materialization
cost, initially reusing the standard wire reader. Protobuf buffering alone did not
produce a useful end-to-end gain. Work remains uncommitted; no package version or
remote publication.
