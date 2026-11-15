# Direct PNG output for Mapsui — 2026-11-15

## Result

Implemented the encoded-output experiment proposed by the
[CPU workload baseline](Request-Workloads-2026-11-15.md). Mapsui now returns
RenderCachedPng bytes directly instead of loading a bitmap and re-encoding it.
In the alternating CPU comparison, median hit latency fell **87–91%** across eight
cases; warm misses improved **9–27%**, fresh misses **6–24%**. This is an encoded
tile-request improvement, not a claim about complete GUI frame rate or GPU speed.

Heavy Bright/z14/512 results:

| Output request | Bitmap API + client encode | Direct PNG | Reduction |
|---|---:|---:|---:|
| Cache hit | 31.30 ms | 2.80 ms | 91.1% |
| Warm-provider miss | 118.48 ms | 89.61 ms | 24.4% |
| Fresh-provider miss | 155.81 ms | 130.11 ms | 16.5% |

## API and implementation

The additive API was defined before implementation as **Renderer.RenderCachedPng**,
returning Task<byte[]> with the same arguments and original/revision overload shapes
as RenderCached. Null represents a missing tile. The returned array is caller-owned;
the canvas remains caller-owned. Existing bitmap signatures/output ownership stay
unchanged. Source revision remains explicit and caller-managed.

[Renderer](../../VectorTileRenderer/Renderer.cs) now shares GetCachePath and PublishPng
between the bitmap and PNG paths. Keys remain version 5, legacy shortened hashes
for null revisions and full hashes for non-null revisions. Both APIs can read the
same files. Publication uses unique temporary files and the existing process-local
lock/atomic move, with best-effort IO/permission failure handling and temp cleanup.

PNG misses render an internally owned bitmap, encode it once, copy the encoded
data into the returned array, and publish that same encoded data. The bitmap and
native image/data resources are disposed before return. If optional publication
fails, usable output still returns; render/encode failures propagate.

PNG hits read original file bytes. **Full decode validation is deliberately retained**:
check the PNG signature, create a Skia codec, and require GetPixels to return Success.
The temporary decoded bitmap is disposed; original PNG bytes return without encoding.
This does not eliminate every decode or implement a header-only trust shortcut.
Non-PNG or undecodable cache entries are removed and rendered again. A test reproduced
an ArgumentNullException from the convenience SKBitmap.Decode(byte[]) overload on a
truncated PNG header; explicit codec creation/result checking fixes that new-path case.

[CacheRequestProfile](../../VectorTileRenderer/CacheRequestProfile.cs) adds HasOutput
and OutputFormat (Bitmap/Png). HasBitmap retains its bitmap-return meaning and is
false for PNG output, even though validation may create a temporary bitmap. Both
APIs emit normal-completion profiles, suppress observer exceptions, and emit no
completed profile on thrown requests. CacheProfileSink.TotalMs includes internal
PNG resource cleanup. Bitmap callback/ownership contracts remain unchanged.

[Mapsui provider](../../Mapsui.Demo.WPF/VectorMbTilesProvider.cs) directly awaits the
PNG API; it retains revision tokens, canvas lifetime, provider reuse/retirement and
failure reporting. No layout, style, dataset, package version or GPU-host change.

## Comparison method

Baseline commit 8e53adf plus uncommitted implementation and workload driver. Old
pattern uses the preserved bitmap API in the same binary, followed by PNG encode
and byte-array creation. New pattern uses RenderCachedPng. This is a same-run API
pattern comparison, not an isolated checkout comparison of two commits.

Same host/dataset/styles/tiles as the preceding baseline: i7-13700K, 24 logical
processors, Windows 11 Pro 10.0.26200, SDK 10.0.400, .NET 10.0.11, x64. Existing
Zurich heaviest native TMS tiles z12/2145/2662 and z14/8580/10646; Basic/Bright,
256/512 output. Dataset/style/font fingerprints and environment are in raw JSON.

Five warmups and 50 measured rounds, six rotating modes: old/new hits, warm misses
and fresh misses. **2,400 measured matrix requests**, plus **300** for a fresh-process
repeat of the noisy Bright/z12/512 case. A ten-round heavy-case pilot preceded them.
One request at a time; new canvas each time; reused styles; provider setup/disposal
inside timings. Fresh means empty decoded-provider cache, not cold filesystem.
OS/SQLite caches, host activity, clock/power state and thermals are uncontrolled.

Both paths produce PNG byte arrays. Old comparison requests dispose their bitmap
inside timing, matching the actual demo lifecycle; the new API does so internally.
This is slightly different from the prior exploratory baseline, which excluded
returned bitmap disposal. Exact reference pixel validation/report writing is outside
timing; validation allocations can influence later GC. Both observers enabled.
Managed allocation counters exclude native memory and verify no thread transition.

Every returned PNG and every published file must decode to the exact CPU reference
pixels. Declared hit/miss, actual CPU state, publication count and no temp leftovers
are checked. Misses use unique revision tokens; both hit modes share a seeded file.
No UI queuing/display/network/GPU timing or adversarial cache-directory guarantee.
Existing sample [provenance limits](../test-data-licensing.md) apply; no binary asset
was added to Git.

Reproduce:

```powershell
dotnet run --project VectorTileRenderer.Benchmarks -c Release -- --request-timing . artifacts/png-comparison 50 "*" png-comparison
dotnet run --project VectorTileRenderer.Benchmarks -c Release -- --request-timing . artifacts/png-comparison-repeat 50 bright-z12-2145-2662-512 png-comparison
```

Local [matrix JSON](../../artifacts/png-comparison/20261115-171335-e2884e02/results.json),
[matrix comparison CSV](../../artifacts/png-comparison/20261115-171335-e2884e02/comparison.csv),
[repeat JSON](../../artifacts/png-comparison-repeat/20261115-171651-71694a23/results.json)
and [repeat CSV](../../artifacts/png-comparison-repeat/20261115-171651-71694a23/comparison.csv)
include raw samples, nearest-rank median/p95, phases, allocations and GC counts.

## Full matrix

Cells show old → new **median milliseconds (reduction)**. These are medians of
complete individual requests, not differences inferred from nested phase medians.

| Style / zoom / size | Hit | Warm miss | Fresh miss |
|---|---:|---:|---:|
| Basic / 12 / 256 | 7.12 → 0.86 (87.9%) | 38.90 → 32.18 (17.3%) | 45.95 → 41.20 (10.3%) |
| Basic / 12 / 512 | 20.02 → 2.35 (88.3%) | 64.49 → 47.30 (26.7%) | 73.23 → 55.66 (24.0%) |
| Basic / 14 / 256 | 7.17 → 0.86 (88.0%) | 44.53 → 38.83 (12.8%) | 81.42 → 72.73 (10.7%) |
| Basic / 14 / 512 | 20.24 → 2.43 (88.0%) | 76.87 → 59.40 (22.7%) | 110.84 → 94.13 (15.1%) |
| Bright / 12 / 256 | 7.15 → 0.93 (87.0%) | 71.78 → 65.57 (8.7%) | 75.62 → 69.63 (7.9%) |
| Bright / 12 / 512 | 24.08 → 2.86 (88.1%) | 114.53 → 94.97 (17.1%) | 136.37 → 108.47 (20.5%) |
| Bright / 14 / 256 | 9.10 → 0.97 (89.4%) | 67.18 → 56.47 (15.9%) | 104.65 → 97.95 (6.4%) |
| Bright / 14 / 512 | 31.30 → 2.80 (91.1%) | 118.48 → 89.61 (24.4%) | 155.81 → 130.11 (16.5%) |

The noisy Bright/z12/512 warm-miss p95 was **227.58 ms old / 234.95 ms new** despite
the better median. Repeat results:

| Bright / 12 / 512 | Old median / p95 ms | New median / p95 ms |
|---|---:|---:|
| Hit | 21.32 / 24.16 | 2.51 / 2.74 |
| Warm miss | 98.97 / 111.47 | 74.36 / 90.22 |
| Fresh miss | 103.95 / 116.24 | 85.84 / 99.74 |

The repeat corroborates median gains, but not every first-run tail is repeatable.
No confidence intervals or per-event trace establish the cause of outliers. Do not
promise universal p95 improvement. Median managed hit allocations declined only
about 4.8 KiB; most output-array allocation remains. Main benefit here is avoided
PNG compression, not a large managed-memory reduction. Provider decode and build/
style allocation costs remain for uncached renders.

## Validation and follow-up

- **116 regressions passed, zero skips**: format/key interoperability, original bytes
  and independent output, null/empty and dataset/revision isolation, full PNG repair,
  observer/finish failures, null output, internal bitmap disposal, failed publication,
  mixed bitmap/PNG requests with pruning. Original bitmap regressions still pass.
- Restore-enabled Release solution/four-target build: zero warnings/errors. Library
  and symbols pack to ignored artifacts/direct-png-package. No publication.
- Exact matrix/repeat pixel and publication checks passed. No new GPU runtime or UI
  visual claim; existing renderer pixels/algorithm are unchanged.
- GitNexus index reports baseline-current. Upstream RenderCachedCore impact HIGH,
  15 dependents across bitmap wrappers, tests, Mapsui/Gmap paths; source verified.
  detect-changes cumulative scope critical/80 flows, including prior uncommitted
  workload changes and adjacent unchanged symbols. FTS/new-symbol graph limits
  remain; tests, builds and pixel checks cover the shared-helper risk.

R-006 has an implemented and measured encoded-output improvement. Next review this
change and check Mapsui reload/revision controls on the new path, then consider a
targeted allocation trace for build/style/decoded-source costs. Existing GPU parity,
hosting, bidi and cross-tile issues remain open. No commit, push or package bump.
