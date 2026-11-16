# Parser stack profile — 2026-11-15

Inspected baseline `f79f882` plus the pending [decode conversion cleanup](Decode-Conversion-2026-11-15.md).
Two independent Release captures, each 1,000 synchronous requests per phase,
identify attribute decoding and protobuf deserialization as the first parser
optimization targets. Geometry decoding follows. GC-poll stacks occupy a large
share of target-thread samples and require separate interpretation.

## Experiment

Windows 11 Pro 10.0.26200, Intel Core i7-13700K, 24 logical processors,
31.3 GiB visible RAM, SDK 10.0.400, runtime 10.0.11, x64.
Local dotnet-trace 10.0.750501, TraceEvent 3.1.21, mapbox-vector-tile 5.3.0.
No rendering backend executes in measured windows.

Checked-in Zurich native tile z14/8580/10646, 11 layers, 16,241 features,
47,827 points. Parser-only reads pre-decompressed in-memory PBF. Conversion-only
reuses a parsed model and a once-bound adapter delegate. Fresh-decode creates a
new MBTiles provider per request and includes database access, decompression,
parsing and conversion. Filesystem cache is warm, each phase has five warmups.
No allocation-tick collector, builds or tests run during capture.

The [workload](../../VectorTileRenderer.Benchmarks/AllocationInvestigation.cs) uses
existing window markers and exact managed allocation counters. The
[analyzer](../../VectorTileRenderer.Benchmarks/ParserCpuReport.cs) selects only the
marked OS thread and records inclusive methods, leaf methods and complete stacks.
Recursive inclusive frames count once per sample. Parser groups are disjoint,
with GC-poll classification taking precedence over managed ancestors.

## Results

Both traces have **zero lost events**. Target-thread samples by phase were
4,107 / 3,581 parser, 691 / 703 conversion, and 10,560 / 8,803 fresh-decode.

| Parser sample group | Capture 1 | Capture 2 |
| --- | --- | --- |
| GC poll on target thread | 2,710 (66.0%) | 2,260 (63.1%) |
| Attribute decoding | 595 (14.5%) | 523 (14.6%) |
| Protobuf deserialization | 449 (10.9%) | 496 (13.9%) |
| Geometry decoding | 199 (4.8%) | 202 (5.6%) |
| Other parser/runtime | 154 (3.7%) | 100 (2.8%) |

Among samples without a GC-poll frame, attributes account for **42.6% / 39.6%**,
protobuf **32.1% / 37.5%**, geometry **14.2% / 15.3%**.
Sampled attribute paths include `Mapbox.Vector.Tile.AttributesParser.Parse` and
attribute-list growth. Protobuf paths include generated `dynamicClass.proto_*`,
`RepeatedSerializer.ReadRepeated`, `ProtoReader.State.FillBuffer`, unsigned-integer
reads and list resizing/copying. Geometry paths include `GeometryParser.ParseGeometry`.
These are resolved runtime call stacks, without an inspection of dependency source.

Fresh-decode stacks also show the adapter, SQLite and monitor waits. Inclusive
parser/adapter counts overlap GC-poll descendants and cannot be added as independent
CPU percentages. Parser allocation stayed 20.31 MiB/request, conversion 7.67 MiB,
fresh-decode 28.27 MiB in both captures, consistent with prior allocation evidence.

## Interpretation and next slice

The Windows collector samples managed thread **wall time**. GC-poll frames indicate
runtime polling/suspension attribution and do not measure GC worker CPU, collection
pause duration or time spent calculating parser output. Removing those samples
provides a narrower view of managed execution locations, still an approximation.
Native frames, JIT inlining and unresolved addresses limit leaf attribution.
See [Microsoft's collector documentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace).

Traced parser windows elapsed 15.79 / 24.24 seconds, conversion 2.38 / 2.55 seconds,
fresh-decode 42.28 / 34.84 seconds. This variability prevents a precise CPU-time or
latency claim. Sample percentages describe this tile and host.

Next inspect the dependency's attribute lookup/value conversion and protobuf
collection construction, then build one bounded candidate preserving the public
model contract. Require typed decoded fingerprints, synthetic regressions and
unprofiled complete-request comparison before accepting a change. The large GC-poll
share supports investigating allocation pressure alongside executed code.

GitNexus status matches committed HEAD. Query `VectorTileParser Parse BaseTileToVector`
returned empty results with `FTS indexes missing — keyword search degraded`.
External parser internals are outside the repository graph. Source inspection of
`PbfTileSource.LoadStream` confirms parser then adapter flow. Empty graph results
provide no caller assurance. This slice changes benchmark tooling and documentation.

## Reproduction and evidence

Commands are in the [benchmark README](../../VectorTileRenderer.Benchmarks/README.md).
Raw local ignored traces are `artifacts/allocation-trace/parser-cpu.nettrace` and
`parser-cpu-repeat.nettrace`. Resolved reports include every aggregated stack and
window count in [capture 1](../../artifacts/allocation-trace/parser-cpu.json) and
[capture 2](../../artifacts/allocation-trace/parser-cpu-repeat.json).

Release benchmark build passed with zero warnings/errors. Both workload/report
entry points completed successfully twice. Regression suite result is recorded in
the handoff. No new binary fixture, package version or remote publication.
