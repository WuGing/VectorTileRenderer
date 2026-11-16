# Direct raw-model conversion — 2026-11-15

Inspected `f79f882` plus pending attribute/decode/profiling work. Follow-up to the
[protobuf buffering experiments](Protobuf-Buffering-2026-11-15.md).

## Implementation and contracts

[PbfParser.ParseRendererTile](../../VectorTileRenderer/Sources/PbfParser.cs) now
deserializes the raw `Mapbox.Vector.Tile.Tile` using the standard protobuf-net
reader, then creates renderer layers/features directly. Each feature's tag pairs
populate its final dictionary. This avoids the decoded Mapbox layer/feature objects,
intermediate attribute lists and feature-ID strings previously created before
renderer conversion. Renderer features have no ID member.

The existing geometry decoder still produces coordinate segments.
[PbfTileSource.AddGeometry](../../VectorTileRenderer/Sources/PbfTileSource.cs) shares
the existing normalization loop between direct and staged conversion, including
the same floating-point division. Geometry-type mapping is also shared.
`LoadStream` selects the direct path for normal raw PBF, gzip and MBTiles loads.

Attribute value types/precedence, duplicate-key last-wins behavior, layer/feature/
geometry ordering, missing-name fallback, extent normalization, mutable-container
ownership and caller-owned stream lifetime are covered by tests. The staged parser
and adapter remain available to the existing benchmark/reference tooling.
The prior private protobuf-reader experiment remains isolated in benchmarks.

## Method

Windows 11 Pro 26200, i7-13700K, 24 logical processors, 31.3 GiB visible RAM,
SDK 10.0.400, .NET 10.0.11, x64. Zurich native tile z14/8580/10646,
11 layers, 16,241 features, 47,827 points. No profiler, builds or tests run alongside
timing. All allocation counts are managed/current-thread.

The [comparison](../../VectorTileRenderer.Benchmarks/FusedConversionComparison.cs)
alternates staged and direct conversion on the same pre-decompressed PBF. Both
return the final renderer model. Ten warmups, 300 pairs main and 500 pairs repeat,
one synchronous thread. Delegates bind once outside measurement. Layer/feature
counts are checked after timing. SQL, decompression and rendering are outside this
isolated window.

Complete CPU Bright 512 PNG requests use the existing six-mode rotating harness,
five warmups, 50 samples/mode main and 30/mode repeat. Main process order is before/
after, repeat is after/before. Fresh includes provider setup, database access,
decompression, full decode/conversion, render, PNG publication and harness-covered
disposal. OS file caches are warm/uncontrolled. Exact pixel validation occurs after
each measured request and may affect subsequent GC.

## Results

| Decode and conversion | Staged | Direct | Reduction |
| --- | --- | --- | --- |
| Main median, 300 samples each | 27.48 ms | 22.83 ms | 16.9% |
| Repeat median, 500 samples each | 29.79 ms | 24.86 ms | 16.5% |
| Main p95 | 41.98 ms | 36.11 ms | 14.0% |
| Repeat p95 | 47.61 ms | 40.87 ms | 14.2% |
| Managed allocation/request | 20.755 MiB | 17.747 MiB | 14.5% |

The complete fresh-decode allocation counter reports 18.04 MiB/request, versus the
preceding 21.05 MiB. The difference includes source/database/decompression costs.
Existing external-parser and staged-adapter allocation windows retain their
defined scope, reporting 20.31 MiB and 7.67 MiB respectively.

| Complete fresh direct-PNG miss | Before | After | Median reduction |
| --- | --- | --- | --- |
| Main median, 50 each | 120.83 ms | 107.43 ms | 11.1% |
| Repeat median, 30 each | 116.59 ms | 104.77 ms | 10.1% |
| Main p95 | 140.62 ms | 146.69 ms | |
| Repeat p95 | 136.52 ms | 137.54 ms | |
| Managed allocation/request | 36.148 MiB | 33.139 MiB | 8.3% |

Complete-request p95 is slightly higher in both runs. Host/process variation and
GC prevent a tail-latency improvement claim. The repeat and alternating isolated
comparison support keeping this simpler direct materialization in normal loading.
These are single-tile/host observations; native memory, retained heap, interactive
UI and GPU performance are outside the measurement scope.

## Validation and scope

- 148 tests pass. Five new cases cover bit-exact direct/staged coordinates at zero,
  normal and maximum extents, independent mutable containers, missing-name empty
  layers, and an allocation regression against the attribute-optimized staged path.
  Existing typed attributes, duplicate keys, malformed data, multipart geometry,
  corrupt/missing source and ownership tests pass.
- Four complete decoded-model fingerprints match, including attribute runtime types,
  values, ordering and coordinate bits. All four main/repeat reference PNGs are
  byte-identical. All 960 measured complete requests pass exact pixel checks;
  published cache PNGs are decoded and compared by the harness.
- Restore-enabled full Release solution build, all four library targets, solution
  tests and pack pass. Third-party notice remains present in the generated package.
- GitNexus index matches committed HEAD. Upstream impact for `LoadStream` and
  `ConvertGeometryType` is HIGH, identifying PBF/MBTiles and validation flows.
  The source confirms their shared demo/provider entry points. Whole pending-diff
  detection reports critical aggregate risk, 9 tracked files / 37 symbols / 30 flows,
  including earlier changes. New untracked parser/test/benchmark files are outside
  those graph counts. Shared normalization plus source inspection and the output/
  regression checks above cover the new path.

## Evidence and next step

Reproduce with `--fused-comparison` in the [benchmark README](../../VectorTileRenderer.Benchmarks/README.md).

- [Main isolated samples](../../artifacts/allocation-trace/fused-comparison.json)
  and [repeat](../../artifacts/allocation-trace/fused-comparison-repeat.json).
- [Complete-request summary CSV](../../artifacts/allocation-trace/fused-request-comparison.csv).
- [Before decoded hashes](../../artifacts/allocation-trace/fused-snapshot-before.json)
  and [after](../../artifacts/allocation-trace/fused-snapshot-after.json).
- Main raw results in `artifacts/fused-before/20261115-215051-0d269489/results.json`
  and `artifacts/fused-after/20261115-215355-f9ab5335/results.json`.
- Repeat raw results in `artifacts/fused-before-repeat/20261115-215526-de7d4201/results.json`
  and `artifacts/fused-after-repeat/20261115-215507-a2e5e66c/results.json`.
- Saved baseline binaries in `artifacts/fused-before-bin`, verified package in
  `artifacts/fused-package`.

Review/checkpoint the accumulated performance changes, then profile the current
complete request to choose a further target. Geometry coordinate segments and raw
protobuf collections remain materialization costs requiring their own measurements.
Work remains uncommitted; R-006 remains In progress.
