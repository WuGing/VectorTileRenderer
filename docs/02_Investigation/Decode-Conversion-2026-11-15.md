# Native decode/conversion allocation cleanup — 2026-11-15

## Result and scope

Split the existing 30.58 MiB fresh native decode into external parsing and renderer
model conversion. The selected heavy tile uses about **20.31 MiB** in the parser and
**9.98 MiB** in our adapter. A small container-initialization change lowers adapter
allocation to **7.67 MiB (23.2% less)** and full fresh decoding to **28.27 MiB
(7.6% less)**. Parser allocation is unchanged. No material latency improvement is
established: complete fresh direct-PNG misses measured **122.33 → 120.04 ms** median,
with higher p95 in the after run. This is allocation-pressure cleanup.

Baseline: `f79f882`, same i7-13700K/Windows 11 Pro 10.0.26200/.NET 10.0.11 x64/SDK
10.0.400 host as the previous measurements. No dependency upgrade/parser replacement.
Keep the earlier [direct PNG](Direct-Png-2026-11-15.md) latency gains separate from
this result. No native-memory, retained-heap, GPU or GUI-frame claim.

## Evidence and implementation

[PbfTileSource.BaseTileToVector](../../VectorTileRenderer/Sources/PbfTileSource.cs)
constructed a new VectorTileFeature, whose field initializers created an empty
dictionary and geometry list. The adapter then replaced both, leaving the original
containers unused. Per-layer feature lists and per-feature geometry lists grew
despite available input counts. The trace's conversion window identifies dictionaries,
dictionary entries, list objects and list backing arrays as material allocation sites.

The adapter now sizes layer/feature lists once and uses a capacity-aware **internal**
[VectorTileFeature constructor](../../VectorTileRenderer/VectorTile.cs). The explicit
public parameterless constructor preserves the previous public signature and
independent mutable empty containers; it delegates with zero capacities. Fields and
properties remain available and no parsed dictionaries or geometry are shared with
the result. Typed attributes are copied using the same last-value-wins assignment;
normalized coordinates use the identical division and ordering. No reciprocal-multiply
or coordinate-math change. Existing default-constructor paths remain unchanged in
behavior; no separately compiled old-consumer binary was exercised.

Installed mapbox-vector-tile 5.3.0 metadata identifies
[repository commit 4c390087380a08a7b6f4c1cc0e46f5e7b34c8cfb](https://github.com/bertt/mapbox-vector-tile-cs/tree/4c390087380a08a7b6f4c1cc0e46f5e7b34c8cfb).
The investigation uses runtime reflection, resolved allocation stacks and our source,
not claims about an uninspected remote implementation. Reflection confirms feature
attributes are a List of KeyValuePair and geometry a List of ArraySegment coordinates;
repeated getter access alone is not evidence of repeated parsing/materialization.

## Allocation method

[AllocationInvestigation](../../VectorTileRenderer.Benchmarks/AllocationInvestigation.cs)
now adds two marked windows to the previous three. Parser-only reads pre-decompressed
bytes with a new stream. Conversion-only uses a pre-parsed tile and a reflection-bound
delegate created outside the window. Fresh decode still includes provider metadata,
SQLite/raw data handling, decompression, parsing, adaptation and provider disposal.
Therefore the isolated phases need not sum exactly to full decode.

Bright style, native Zurich TMS z14/8580/10646, 512 pixels. Five warmups and 100
iterations per marked operation; profiling observers off, no PNG encoding or image
comparison inside windows. dotnet-trace 10.0.750501 gc-verbose plus informational
phase markers; analyzer uses TraceEvent 3.1.21 already in benchmark dependencies.
Before/after traces each report **zero lost events**. Type/stack sample weights are
approximate; the table uses exact current-thread managed allocation counters, guarded
against thread transitions. Native allocations and other threads are excluded.

| Phase | Before MiB/request | After MiB/request | Reduction |
|---|---:|---:|---:|
| Parser only | 20.306 | 20.306 | None |
| Conversion only | 9.984 | 7.670 | 23.2% |
| Full fresh native decode | 30.583 | 28.269 | 7.6% |
| Warm render | 14.082 | 14.081 | Essentially unchanged |
| Warm build without drawing | 10.245 | 10.245 | Unchanged |

The conversion window's dictionary sample weight falls by about half, and feature
array-growth samples shrink, consistent with removing unused containers and growth.
Do not treat interval-weight differences as an exact per-object allocation census.
External parser samples still include UInt32/coordinate/tag arrays and iterators;
changing that library would require a separate experiment and compatibility plan.

## Complete request measurements

Untraced sequential before/after processes use the existing six-mode PNG comparison
on the same heavy tile/style/size, five warmups then 50 samples per mode. Old pattern
is bitmap-cache API plus caller PNG encoding; direct pattern returns PNG bytes.
All modes include canvas/provider setup/disposal and relevant bitmap disposal.
Styles reused, new canvas per request, OS/SQLite caches and host activity uncontrolled.
Fresh means a new decoded-provider cache, not cold disk. Exact returned/published
PNG pixel validation is outside timing; it can affect subsequent GC.

| Mode | Before median / p95 ms | After median / p95 ms |
|---|---:|---:|
| Warm miss + caller PNG | 115.68 / 128.56 | 118.35 / 163.85 |
| Fresh miss + caller PNG | 151.11 / 165.87 | 151.79 / 183.16 |
| Hit + caller PNG | 31.48 / 32.95 | 31.54 / 38.94 |
| Direct PNG warm miss | 88.20 / 97.64 | 90.94 / 130.80 |
| Direct PNG fresh miss | 122.33 / 135.97 | 120.04 / 167.28 |
| Direct PNG hit | 2.85 / 3.28 | 2.84 / 3.52 |

Fresh direct-PNG managed allocation drops **45.683 → 43.369 MiB/request**. Warm
requests/hits do not decode native data and their allocations stay effectively
unchanged. These timings are roughly flat/noisy, not a justified latency speedup;
the after run has noisier tails. No confidence interval or trace proves the cause.

## Correctness and compatibility validation

- **120 regressions pass**, zero skips. New project-authored synthetic PBF covers
  typed/duplicate attributes, multipoints, two line parts, polygon closure, empty
  layer, normalized coordinates, independent feature containers and caller-owned
  stream lifetime. The default public constructor remains reflectively available.
- New [DecodeSnapshots](../../VectorTileRenderer.Benchmarks/DecodeSnapshots.cs) records
  all layers/features, sorted typed attribute values, normalized extent/type and every
  coordinate's exact double bits. Before/after fingerprints match for all four cases:

| Fixture | Layers | Features | Points | SHA-256 fingerprint |
|---|---:|---:|---:|---|
| Zurich gzip PBF | 11 | 16,241 | 47,827 | EAAB41BC152F918899CC199054303517B8383C497EF21C053CF25425D7674DDC |
| New York raw PBF | 12 | 4,825 | 27,066 | 6D5483FB1AA44329338CBC0E8F82727E31AEC4FAB85983B2FA73E291C61BB5AB |
| Zurich z12/2145/2662 | 12 | 5,264 | 16,307 | BB5C3C48C618831220FF7EE738F0FCE505E9BD2C6A2A52DED6628CFDD976B31A |
| Zurich z14/8580/10646 | 11 | 16,241 | 47,827 | EAAB41BC152F918899CC199054303517B8383C497EF21C053CF25425D7674DDC |

- The focused before/after reference PNG is byte-identical; every measured output
  and published PNG passed exact CPU pixel checks. No new interactive UI/GPU pass.
- Restore-enabled full Release solution/all four library targets: zero warnings/errors.
  Library and symbols pack passed to ignored artifacts/decode-conversion-package.
- GitNexus index refreshed from 8e53adf to f79f882; sandbox registry write failed,
  escalated retry succeeded. Adapter upstream HIGH includes both MBTiles providers
  and validation workloads. VectorTileFeature impact CRITICAL/17 direct graph entries,
  including import references and test/render consumers. Source inspection plus
  public-constructor tests, deep model fingerprints, full build and regressions cover
  that shared-model risk. New helpers/constructor remain unindexed; graph absence
  is not proof of no callers. Scope includes no renderer/backend replacement.
- Existing [data provenance limits](../test-data-licensing.md) remain; fixtures used
  locally, no new binary dataset committed. Synthetic test bytes are authored in code.

## Local evidence and next step

Use the commands in the [benchmark README](../../VectorTileRenderer.Benchmarks/README.md).
Allocation trace/report before: artifacts/allocation-trace/decode-before.nettrace and
[decode-before.json](../../artifacts/allocation-trace/decode-before.json); after:
artifacts/allocation-trace/decode-after.nettrace and
[decode-after.json](../../artifacts/allocation-trace/decode-after.json).

- [Exact allocation comparison CSV](../../artifacts/allocation-trace/decode-allocation-comparison.csv).
- [Complete request comparison CSV](../../artifacts/allocation-trace/decode-timing-comparison.csv).
- [Decoded model before](../../artifacts/allocation-trace/decode-snapshot-before.json)
  and [after](../../artifacts/allocation-trace/decode-snapshot-after.json).
- Raw request samples: artifacts/decode-conversion-before/20261115-203910-af89652b/results.json
  and artifacts/decode-conversion-after/20261115-204205-084bf60f/results.json.

Review/checkpoint this bounded change. Further latency work should CPU-profile the
parser and build/style path; reducing allocations alone has not yielded a clear
elapsed-time gain here. Do not replace the parser or introduce pooled/shared mutable
geometry based solely on allocation samples. R-006 remains In progress, existing
GPU/bidi/cross-tile limits remain open. No package bump, commit, push or publication.
