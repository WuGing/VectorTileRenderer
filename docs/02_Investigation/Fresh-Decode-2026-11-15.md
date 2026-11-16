# Fresh fetching and direct geometry decoding — 2026-11-15

## Result

Fresh complete direct-PNG requests measure 9–10% faster in two alternating
comparisons. Managed allocation falls approximately 2.36 MiB per heavy fresh
request. All eight reference images and four typed decode snapshots match exactly.
The current Mapsui build is published for the requested hand test.

Baseline `f79f882` plus pending work through
[the post-submission checkpoint](Post-Submission-Profile-2026-11-15.md), library
`B942706ABA6FF1B32485AAA784187220FBBE9C1024A22EF1F734FE46911B436B`.
Final candidate
`934E88F56239431E7962115595CCFE58F2667AD470EFD06EFC7E95DA4C32F2CE`.
Changes remain uncommitted.

## Attribution and selected change

The [fresh-decode attribution command](../../VectorTileRenderer.Benchmarks/FreshDecodeTiming.cs)
rotates isolated operations on native Zurich z14/8580/10646, 16,241 features,
288,214 compressed bytes / 527,024 plain bytes. Ten warmups, 100 samples/operation.

| Baseline operation | Median ms | Managed bytes |
|---|---:|---:|
| Provider construction and metadata | 0.35 | 16,053 |
| Warmed SQLite raw tile read | 0.24 | 290,408 |
| Gzip-to-buffer pilot | 1.22 | 1,966,608 |
| Protobuf model from plain PBF | 5.86 | 5,960,773 |
| Dependency geometry parsing only | 0.72 | 4,186,248 |
| Full plain renderer decode | 12.02 | 18,608,812 |
| Full gzip renderer decode | 14.28 | 18,609,045 |
| Fresh provider plus decode/disposal | 14.82 | 18,916,258 |

These operations overlap and must be interpreted separately. The inflation pilot
materializes a buffer, while production streams gzip. SQL/metadata are small on
this warmed local dataset. Geometry intermediates supply a substantial allocation
target alongside protobuf/model work.

[PbfGeometryDecoder](../../VectorTileRenderer/Sources/PbfGeometryDecoder.cs) adapts
the installed mapbox-vector-tile 5.3.0 GeometryParser algorithm to write normalized
renderer points directly into final segment lists. It avoids the temporary
Coordinate array, segment descriptors and subsequent geometry copy. The normal
[PbfParser.ParseRendererTile](../../VectorTileRenderer/Sources/PbfParser.cs) uses it.
The dependency geometry parser remains the reference for staged/experimental paths.
Attribution is recorded in [third-party notices](../../VectorTileRenderer/THIRD-PARTY-NOTICES.txt).

The decoder preserves command order, long cumulative deltas, ZigZag behavior,
segment boundaries, point/line/polygon closing behavior, zero extents, division
rounding and geometry-before-attribute error evaluation. Unknown command behavior
and malformed input exception types are checked against the installed dependency.
The standard protobuf reader and production streaming gzip path remain in use.

## Malformed no-progress command

Differential testing reproduced a stall in the existing reference decoder on a
zero-count ClosePath followed by more input when no coordinate can be appended.
The unsigned remaining count underflows, producing billions of no-progress loop
iterations. The stalled reference test process was stopped, and the differential
generator now excludes that specific unbounded reference case.

The new production decoder rejects that no-progress condition with
InvalidDataException. Two focused tests cover Point and Polygon inputs. This is
an intentional malformed-input behavior correction. Other bounded malformed cases
are compared for exception-type parity. Valid-input coordinate bits remain exact.

## Complete-request measurements

Windows 11 Pro 26200, i7-13700K, 24 logical processors, approximately 31.3 GiB RAM,
SDK 10.0.400, .NET 10.0.11 x64, Skia assembly 3.119.0.0 CPU. Checked-in Zurich
MBTiles and Bright style/fonts, native z14/8580/10646, 512px. Warmed/uncontrolled
OS file caches. Timings, builds and tests run sequentially. No forced GC.

Two assemblies run in one process, alternating implementation order each round
and rotating fresh/warm/hit modes. Each implementation owns its style/provider/cache.
Five warmups and 60 samples/mode/implementation, two runs. Observers disabled.
Timing includes setup, reflected invocation, full request, encoding/publication
and disposal. Pixels and all publications are checked outside timers and may
influence later GC. Host, JIT and shared-heap interactions limit generalization.

| Run/mode | Baseline median ms | Candidate median ms | Lower elapsed | Baseline p95 ms | Candidate p95 ms |
|---|---:|---:|---:|---:|---:|
| First fresh miss | 69.16 | 63.09 | 8.8% | 87.05 | 84.60 |
| Repeat fresh miss | 71.53 | 64.34 | 10.1% | 89.33 | 89.19 |
| First reused-provider miss | 39.40 | 37.65 | 4.4% | 54.56 | 55.98 |
| Repeat reused-provider miss | 40.04 | 36.81 | 8.1% | 54.85 | 56.55 |

Fresh mean managed allocation falls approximately 27.05 → 24.57 MB/request (9.2%),
about 2.48 million bytes / 2.36 MiB. Warm allocation stays approximately 8.13 MB.
The decoded-tile cache bypasses the new decoder for warm requests. Their timing
changes occur in a mixed-mode process with shared GC/JIT and host effects.
Hit medians remain approximately 2.3–2.4 ms. Fresh p95 improvement is modest.

Final isolated plain/gzip/fresh-provider medians are 9.84/11.66/13.02 ms.
Baseline repeats are 11.32/13.06/14.18 ms, alongside initial 12.02/14.28/14.82 ms.
Fresh-provider allocation falls 18.92 → 16.44 MB (13.1%). The allocation reduction
is more stable than separate-process isolated timing. Native/retained memory and
actual GPU/UI/concurrent performance require their own measurements.

## Compatibility and build validation

[Geometry tests](../../VectorTileRenderer.Tests/PbfGeometryDecoderTests.cs) cover
four geometry types and unknown enum values, multipart/close commands, invalid
commands, four extents including zero, 2,000 bounded malformed word sequences,
large cumulative deltas and exact double bits. A public loader test checks geometry
failure before invalid attribute values. Existing typed-value, duplicate-key,
gzip/plain, corrupt/missing source, wire-format and ownership tests pass.

The final eight-case CPU matrix covers Zurich z12/z14, Basic/Bright, 256/512px,
six modes, 20 samples/mode. All 960 timed outputs and publications match exactly.
All eight reference PNG hashes match the prior submission checkpoint. Two final
paired runs add 720 exact-pixel requests. Four typed decode snapshots, including
standalone gzip/plain PBF and both native MBTiles tiles, are identical.

- Restore-enabled Release solution build passes with zero warnings/errors.
- All 205 Release tests pass.
- All 20 Debug geometry/parser tests pass.
- All-target library packaging and Mapsui framework-dependent publish pass.
- Published renderer hash matches the measured/tested candidate.

GitNexus reports the committed index current at HEAD. Upstream LoadStream impact
is HIGH, nine results / four processes / two modules, including MBTiles paths and
PBF tests. Direct source verifies the current raw/protobuf/geometry flow and the
new unindexed decoder. Shared source paths affect all demos and backends.

## Requested hand-test build

Published folder `artifacts/manual-testing-current` includes Mapsui.Demo.WPF.exe,
its dependencies, Zurich tiles, styles and 48 fonts. It uses the installed .NET
Desktop Runtime 10 on Windows x64. BUILD-INFO.txt identifies this validated build.
Open the executable from that folder and retain its accompanying files/folders.

Automatic launch through the desktop tool timed out waiting for app approval.
A subsequent window inventory showed no Mapsui window. Startup/interaction checks
are pending the user's manual pass. The executable/assets/hash are verified locally.
See [manual launch and checklist](../Manual-Validation.md).

## Reproduction and local evidence

See the [benchmark README](../../VectorTileRenderer.Benchmarks/README.md).

```powershell
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --fresh-decode-timing . artifacts/fresh-decode-final-after.json 100
dotnet VectorTileRenderer.Benchmarks/bin/Release/net10.0/VectorTileRenderer.Benchmarks.dll --paired-request-comparison . artifacts/fresh-decode-before-bin/WuGing.VectorTileRenderer.dll artifacts/fresh-decode-final-paired.json 60
dotnet publish Mapsui.Demo.WPF/Mapsui.Demo.WPF.csproj -c Release --self-contained false -o artifacts/manual-testing-current
```

- Attribution `artifacts/fresh-decode-before.json`, `fresh-decode-before-repeat.json`,
  `fresh-decode-final-after.json`.
- [Final paired requests](../../artifacts/fresh-decode-final-paired.json),
  [repeat](../../artifacts/fresh-decode-final-paired-repeat.json),
  [summary](../../artifacts/fresh-decode-paired-summary.csv).
- [Final matrix](../../artifacts/fresh-decode-final-matrix/20261116-052636-609980f8/results.json)
  and [reference checks](../../artifacts/fresh-decode-pixel-equality.json).
- Typed snapshots `artifacts/fresh-decode-snapshot-before.json` and
  `fresh-decode-snapshot-final.json`.
- [Hand-test build information](../../artifacts/manual-testing-current/BUILD-INFO.txt).

Next collect the requested manual feedback and profile current complete requests
before selecting another optimization. R-006 remains In progress.
