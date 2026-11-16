# Protobuf buffering experiments — 2026-11-15

Inspected `f79f882` plus the pending [attribute optimization](Attribute-Parsing-2026-11-15.md).
The direct reader is retained as an isolated benchmark experiment after complete
requests failed to demonstrate a gain. The normal loader uses the standard
protobuf-net stream deserializer with the attribute-optimized adapter.

## Source evidence and candidates

protobuf-net 3.2.56's `ReadBuffer<T>` rents arrays from `ArrayPool<T>.Shared`, grows
by renting/copying/returning, then returns its final array on disposal. Repeated
serializers fill that buffer and append its segment to destination lists. Removing
this staging exchanges pooled copying for destination-list growth. Allocation
samples alone cannot establish that this trade improves complete requests.
[Exact-version buffer source](https://github.com/protobuf-net/protobuf-net/blob/dfdfce61a739cfd76f05fcdacf8a4b3b9e94e684/src/protobuf-net.Core/Internal/ReadBufferT.cs),
[repeated serializer](https://github.com/protobuf-net/protobuf-net/blob/dfdfce61a739cfd76f05fcdacf8a4b3b9e94e684/src/protobuf-net.Core/Serializers/RepeatedSerializer.cs).

The experiment uses a private `RuntimeTypeModel` and custom `ISerializer<T>` readers
for Tile, Layer and Feature. Feature tags/geometry append directly from packed or
unpacked scalars; layers/features/keys/values append directly to their owning
collections. Value decoding still uses the generated dependency contract. Unknown
fields use `AppendExtensionData`. Protobuf's reader retains responsibility for
message limits, wire validation and nested-message boundaries. Writers throw because
the private model is read-only. Global default-model configuration is independent.
[Official custom-serializer example](https://github.com/protobuf-net/protobuf-net/blob/dfdfce61a739cfd76f05fcdacf8a4b3b9e94e684/src/protobuf-net.Test/CustomSerializers.cs).

The [isolated adapter](../../VectorTileRenderer.Benchmarks/ProtobufExperiment/ProtobufReadExperiment.cs)
matches production attribute/model construction so the comparison targets protobuf
reading. Its three reader types live beside it. Portable
[wire tests](../../VectorTileRenderer.Tests/PbfWireTests.cs) link those experiment
sources directly, avoiding a BenchmarkDotNet dependency in the test project.

A second pilot copied input into one MemoryStream and used the standard
`Serializer.Deserialize<Tile>(ReadOnlyMemory<byte>)` entry point. This trades input
buffer allocation for in-memory reader execution. Its source/binaries remain local
experiment artifacts.

## Method

Same Windows 11 Pro 26200 / i7-13700K / 24 logical processors / 31.3 GiB host,
.NET 10.0.11, SDK 10.0.400, x64. Zurich z14/8580/10646, 11 layers, 16,241 features,
47,827 points. Release, synchronous single-thread requests, no profiler, no parallel
build/test workload during timing. Pre-decompressed PBF for isolated parser runs,
10 warmups per implementation, alternating baseline/candidate order every round.
Baseline is the preceding attribute-optimized parser, loaded from a saved assembly
in a separate load context with shared dependency types. Final benchmark command
also compares the isolated reader directly against the current production adapter.

Complete CPU Bright 512 PNG runs use the existing rotating six-mode harness with
five warmups, 50 samples/mode main, 30/mode repeat. Main order is baseline then
candidate; repeat reverses process order. Fresh requests include provider creation,
SQL, decompression, decode/conversion, rendering, PNG publication and covered disposal.
OS file caches are warm and uncontrolled. Pixel checks happen outside timers and
can influence subsequent GC. All reported allocation counts are managed/current-thread.

## Results

| Isolated parser comparison | Baseline median | Candidate median | Candidate change |
| --- | --- | --- | --- |
| Feature-only pilot, 100 pairs | 14.81 ms | 13.80 ms | 6.8% lower |
| Full direct reader, 300 pairs | 14.70 ms | 14.18 ms | 3.5% lower |
| Full direct reader, 1,000 pairs | 15.82 ms | 15.03 ms | 5.0% lower |
| Isolated final command, 100 pairs | 16.37 ms | 15.05 ms | 8.1% lower |
| Standard in-memory reader pilot, 100 pairs | 15.08 ms | 13.78 ms | 8.6% lower |

Full direct-reader allocation increases from 13.085 to 13.189 MiB/parse (0.8%).
Feature-only allocation is 13.430 MiB. The in-memory pilot allocates 13.587 MiB.
Destination-list growth and the extra input buffer explain the direction of these
changes. These results describe this tile/host and include GC effects.

| Complete fresh direct-PNG miss | Baseline median | Candidate median | Baseline / candidate p95 |
| --- | --- | --- | --- |
| Full direct reader main, 50 each | 114.24 ms | 121.40 ms | 132.23 / 183.21 ms |
| Full direct reader repeat, 30 each | 117.95 ms | 126.60 ms | 162.24 / 174.93 ms |
| Standard in-memory pilot, 30 each | 130.15 ms | 128.70 ms | 224.15 / 209.21 ms |

Direct-reader complete-request medians are 6.3% / 7.3% higher. Managed allocation
increases from 36.148 to 36.252 MiB/request. The memory pilot's median is 1.1% lower,
with allocation increasing to 38.023 MiB (5.2% more).

Host/process variation also appears in warm modes and tails. These measurements
support leaving both candidates out of production. They establish no useful
end-to-end speedup from this buffering change, and do not isolate the cause of the
slower direct-reader runs. No native-memory, UI or GPU claim follows from them.

## Compatibility and validation

- 143 tests pass, comprising the previous 126 plus 17 experiment cases. Coverage
  includes packed/unpacked/repeated scalars, empty packed fields, scalar overwrite
  order, enum values, unknown fields/groups at feature/layer/root, malformed lengths/
  varints, default/empty messages, concurrent independent features, and 100 seeded
  randomized models through capped three-byte reads. Round-trip bytes are compared
  through the default serializer, exercising unknown-field retention.
- The direct reader accepts interleaved packed/unpacked segments in wire order.
  The default dependency reader rejects the tested contiguous mixed-wire sequence.
  This behavior difference is confined to the isolated experiment.
- All four typed decoded-model fingerprints match baseline/candidate and the final
  production loader. All six reference PNGs are byte-identical. All 1,320 measured
  complete requests pass exact pixel checks; published cache PNGs are decoded and
  compared by the harness.
- Restore-enabled full Release solution build passes, including all four library
  targets and Windows demos. Solution tests and pack pass. Package inspection
  confirms the third-party notice entry and library-only contents. Experiment
  classes compile in the benchmark/test projects.
- GitNexus status matches committed HEAD; upstream `LoadStream` impact identifies
  both MBTiles providers, PBF tests, and validation workloads. Whole pending-diff
  detection reports critical aggregate risk, 9 tracked files / 34 symbols / 30 flows,
  including earlier changes. New untracked experiment/test files are outside that
  graph result. Source inspection and the validations above cover their scope.

## Evidence and next step

Reproduction uses `--protobuf-comparison` in the [benchmark README](../../VectorTileRenderer.Benchmarks/README.md).
Optional saved-baseline assembly support is also available to the parser comparison.

- [300-pair direct-reader samples](../../artifacts/allocation-trace/protobuf-parser-comparison.json),
  [1,000-pair repeat](../../artifacts/allocation-trace/protobuf-parser-repeat.json),
  [final isolated command](../../artifacts/allocation-trace/protobuf-isolated-confirmation.json).
- [Feature-only pilot](../../artifacts/allocation-trace/protobuf-parser-pilot.json)
  and [standard in-memory pilot](../../artifacts/allocation-trace/protobuf-memory-pilot.json).
- [Complete-request summary CSV](../../artifacts/allocation-trace/protobuf-request-comparison.csv).
- [Before fingerprints](../../artifacts/allocation-trace/protobuf-snapshot-before.json),
  [direct-reader fingerprints](../../artifacts/allocation-trace/protobuf-snapshot-after.json),
  [final production fingerprints](../../artifacts/allocation-trace/protobuf-snapshot-final.json).
- Raw main request results are in `artifacts/protobuf-before/20261115-213127-35c8e1f7`
  and `artifacts/protobuf-after/20261115-213545-0203701e`. Repeat directories are
  `artifacts/protobuf-before-repeat/20261115-213815-54137bca` and
  `artifacts/protobuf-after-repeat/20261115-213753-0468344d`.
- Memory-pilot directories are `artifacts/protobuf-memory-before/20261115-214003-38013622`
  and `artifacts/protobuf-memory-after/20261115-213939-3b6395be`.
- Local source/binary snapshots are `artifacts/protobuf-direct-source`,
  `artifacts/protobuf-direct-bin`, `artifacts/protobuf-before-bin` and
  `artifacts/protobuf-memory-source`.

Next define and measure a fused adapter that converts raw protobuf features to
renderer features directly, removing an intermediate decoded model. Reuse the
standard wire reader initially so the experiment isolates that materialization.
Require the same typed-model fingerprints, ownership checks and complete-request
measurements. Current work remains uncommitted; R-006 remains In progress.
