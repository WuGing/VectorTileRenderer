# Attribute parsing and protobuf collections — 2026-11-15

Inspected `f79f882` plus pending decode/profiling changes. Follow-up to
[parser stack profiling](Parser-Cpu-2026-11-15.md).

## Dependency source evidence

The installed mapbox-vector-tile 5.3.0 package identifies commit
`4c390087380a08a7b6f4c1cc0e46f5e7b34c8cfb`. Its attribute parser enumerates tags
twice through indexed LINQ filters, materializes odd/even lists, then grows an
initially empty key/value list. Key lookup itself already indexes the layer table.
The waste lies in enumerators, temporary arrays/lists and growth/copying.
[Attribute parser source](https://github.com/bertt/mapbox-vector-tile-cs/blob/4c390087380a08a7b6f4c1cc0e46f5e7b34c8cfb/src/AttributesParser.cs),
[tag filters](https://github.com/bertt/mapbox-vector-tile-cs/blob/4c390087380a08a7b6f4c1cc0e46f5e7b34c8cfb/src/ExtensionMethods/IEnumerableExtensions.cs).

The dependency's parser also grows decoded layer/feature lists without setting
their known capacities. Its generated protobuf model initializes get-only lists
for raw features, tags, geometry, keys and values.
[Parser](https://github.com/bertt/mapbox-vector-tile-cs/blob/4c390087380a08a7b6f4c1cc0e46f5e7b34c8cfb/src/VectorTileParser.cs),
[protobuf model](https://github.com/bertt/mapbox-vector-tile-cs/blob/4c390087380a08a7b6f4c1cc0e46f5e7b34c8cfb/src/VectorTile.cs).

Installed protobuf-net 3.2.56 identifies commit
`dfdfce61a739cfd76f05fcdacf8a4b3b9e94e684`. `ReadRepeated` fills a temporary
`ReadBuffer<T>`, then the list serializer calls `List<T>.AddRange` with its segment.
Packed varints are decoded one at a time into that buffer. Fixed-width packed
values have a count-based capacity optimization, while varints have only a byte
length and take the incremental path. This explains the sampled buffer/list/copy
work. The inspected path exposes no per-request capacity hint for these get-only
model lists. A protobuf collection change would require dependency/model or custom
serializer work and warrants a separate candidate and compatibility comparison.
[Repeated serializer](https://github.com/protobuf-net/protobuf-net/blob/dfdfce61a739cfd76f05fcdacf8a4b3b9e94e684/src/protobuf-net.Core/Serializers/RepeatedSerializer.cs),
[packed reads and buffers](https://github.com/protobuf-net/protobuf-net/blob/dfdfce61a739cfd76f05fcdacf8a4b3b9e94e684/src/protobuf-net.Core/ProtoReader.State.ReadMethods.cs).

## Implemented candidate

[PbfParser.Parse](../../VectorTileRenderer/Sources/PbfParser.cs) uses the existing
`Serializer.Deserialize<Tile>` and `GeometryParser.ParseGeometry`, reads tags in
pairs, and preallocates decoded attribute/layer/feature collections. It returns
the same dependency model consumed by
[PbfTileSource.BaseTileToVector](../../VectorTileRenderer/Sources/PbfTileSource.cs).
Normal PBF, gzip and MBTiles loading enter through `LoadStream`.

The local implementation preserves attribute value types and precedence
(bool, double, float, int, string, sint, uint), duplicate key ordering, missing-name
fallback, IDs, geometry/extent and exception types for invalid tag pairs. Containers
are request-owned. The adapted source's MIT attribution is included in
[third-party notices](../../VectorTileRenderer/THIRD-PARTY-NOTICES.txt), verified at
the root of the generated NuGet package.

## Measurements

Same i7-13700K / 24 logical processors / Windows 11 26200 / 31.3 GiB host,
SDK 10.0.400, .NET 10.0.11, x64 as the prior profile. Zurich native tile
z14/8580/10646, 11 layers, 16,241 features, 47,827 points. Parser comparison uses
pre-decompressed bytes, alternates old/candidate order per round, 10 warmups and
300 measured parses per implementation, one thread, Release, no profiler.
Result validation executes outside the measured window.

| Parser metric | Dependency | Candidate | Reduction |
| --- | --- | --- | --- |
| Median | 25.42 ms | 16.28 ms | 36.0% |
| p95 | 37.59 ms | 23.07 ms | 38.6% |
| Managed bytes/request | 20.306 MiB | 13.085 MiB | 35.6% |

Full fresh decode managed allocation is 21.05 MiB/request versus the prior
28.27 MiB, about 25.5% less. The original external-parser allocation window reports
20.31 MiB, and conversion-only reports 7.67 MiB, as expected for their isolated scope.

Complete CPU Bright 512 direct-PNG requests use the existing rotating six-mode
harness, new providers for fresh requests, warm OS file caches, five warmups,
50 measured samples/mode in the main run and 30/mode in the repeat. Runs are
sequential old/new processes and include provider/canvas setup, full request,
PNG publication, and disposal covered by that harness.

| Fresh direct-PNG miss | Before | After | Reduction |
| --- | --- | --- | --- |
| Main median | 126.97 ms | 110.37 ms | 13.1% |
| Repeat median | 120.17 ms | 110.02 ms | 8.4% |
| Main p95 | 178.76 ms | 131.23 ms | 26.6% |
| Repeat p95 | 145.74 ms | 127.72 ms | 12.4% |
| Managed allocation | 43.369 MiB | 36.148 MiB | 16.7% |

Warm/hit medians also drifted across processes. This limits attribution of the
entire end-to-end reduction to the candidate. The alternating parser comparison
provides the more controlled evidence of parser improvement. Results describe one
heavy tile and host, with uncontrolled filesystem caches and GC. Tail reductions
are observations of these samples. Native memory and interactive UI timing remain
outside the measurement scope.

## Compatibility and validation

- 126 tests pass via `dotnet test VectorTileRenderer.sln -c Release --no-restore`.
  [Parity tests](../../VectorTileRenderer.Tests/PbfParserParityTests.cs) cover seven
  value types/defaults/precedence, invalid tag lengths/indices, referenced empty
  values, stream ownership and an attribute-heavy managed-allocation regression.
  Existing synthetic multipart/duplicate-key conversion and corrupt-tile tests pass.
- Four fixture decoded fingerprints are identical before/after, including typed
  attribute values, ordering and coordinate bits. Main reference PNG is byte-identical.
  All 960 measured complete requests pass exact pixel checks. Cache publications
  are decoded and checked by the harness.
- Restore-enabled full Release solution build passes, including all four library
  frameworks and Windows examples. Pack succeeds with notice file explicitly
  verified in `artifacts/attribute-package-verified`.
- GitNexus index matches committed HEAD. Upstream `BaseTileToVector` impact is HIGH,
  with `LoadStream` and both MBTiles providers in its flows. Source confirms demo,
  benchmark and validation callers. `detect-changes --scope all` reports critical
  aggregate risk across 8 tracked files / 34 symbols / 30 flows, including earlier
  pending work. New untracked source/tests are outside that report. Full builds,
  parity tests, fingerprints and complete CPU output checks cover this shared path.

## Local evidence and next step

- [Alternating parser samples](../../artifacts/allocation-trace/attribute-parser-comparison.json).
- [Complete-request summary CSV](../../artifacts/allocation-trace/attribute-request-comparison.csv).
- [Before model hashes](../../artifacts/allocation-trace/attribute-snapshot-before.json)
  and [after model hashes](../../artifacts/allocation-trace/attribute-snapshot-after.json).
- Main raw results in `artifacts/attribute-before/20261115-211859-c660588f/results.json`
  and `artifacts/attribute-after/20261115-212106-99072cfb/results.json`.
- Repeat raw results in `artifacts/attribute-before-repeat/20261115-212200-32e2a3fd/results.json`
  and `artifacts/attribute-after-repeat/20261115-212220-b1046377/results.json`.
- Exact dependency source downloads are local under `artifacts/parser-source`.

Next profile the current parser path, then evaluate protobuf materialization if
it still leads. A custom serializer or fused raw-model conversion needs its own
contract definition and differential validation. Current production work is
uncommitted. R-006 remains In progress.
