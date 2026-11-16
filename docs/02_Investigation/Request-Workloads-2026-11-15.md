# CPU request workload measurements — 2026-11-15

## Findings and recommended next slice

The measured costs are broader than drawing: decoded-provider reuse avoids repeated
fetch/decode work, building/styling visual layers takes a large portion of warm
rendering, and PNG output is material. CPU finish is small in this workload; that
is not a statement about GPU readback.

The most bounded next experiment is **an encoded-output cache path for Mapsui**.
The current [provider](../../Mapsui.Demo.WPF/VectorMbTilesProvider.cs) loads a cached
PNG through the bitmap API and encodes it to PNG bytes again. In the repeated heavy
Bright/512 case, the bitmap hit took **3.14 ms**, versus **31.44 ms** including caller
encoding; the caller encoding phase alone had a **28.14 ms** median. On a warm miss,
cache encoding/publication cost **29.57 ms**; the client-output mode adds another
encoding phase (**28.24 ms**). Preserve existing bitmap APIs, source identities,
corruption recovery, atomic publication and ownership when defining that experiment.
These are measured costs, not a promised speedup for an unimplemented API.

Provider reuse is already important to preserve: heavy Bright/512 direct rendering
was **58.16 ms warm versus 101.89 ms fresh** in the repeat. A new provider's metadata
setup was about half a millisecond; its fetch/decode phase was **36.67 ms**, versus
about **0.001 ms** for the warm decoded cache. Do not recreate the provider per tile.
The demo already retains providers until reload/style/revision changes.

Warm renders allocate approximately **12–20 MiB** managed memory per request;
fresh providers raise this to **22–50 MiB**. These allocations justify a subsequent
decode/build/style allocation investigation. They do not identify particular object
types or native allocations. No optimization or alternative backend was implemented.

## Workload, host and reproducibility

Renderer baseline: `8e53adf` (after build fix `03a6a70`). Only the
[benchmark entry point](../../VectorTileRenderer.Benchmarks/Program.cs), new
[RequestTiming harness](../../VectorTileRenderer.Benchmarks/RequestTiming.cs) and
measurement documentation changed in this slice. The existing BenchmarkDotNet
entry path remains available; this is a sequential exploratory request driver,
not a BenchmarkDotNet statistical experiment.

- Intel Core i7-13700K, 24 logical processors; Windows 11 Pro 10.0.26200,
  approximately 31.3 GiB OS-visible RAM. SDK 10.0.400, runtime .NET 10.0.11, x64.
- Existing Zurich MBTiles: 36,790,272 bytes;
  SHA-256 `407F3DE95BE97D33DB7653CADE6AFD3B3BE9B39DCBFC2756FA049464DD718ED1`.
- Deterministic selection: largest compressed native tile at each zoom, tie by
  column/row. Zoom 12: TMS x=2145, y=2662, 74,835 compressed bytes, 5,264 features.
  Zoom 14: TMS x=8580, y=10646, 288,214 bytes, 16,241 features.
- Basic and Bright styles, 256/512-pixel output. Style/font fingerprints, Skia
  version and raw samples are in JSON. Size changes also affect the renderer's
  effective style zoom; this is not a pure pixel-area scaling experiment.
- One request at a time, five warmups plus 50 samples per case/mode, rotated mode
  order. Eight cases × seven modes = **2,800 measured requests**. A fresh-process
  repeat of the noisy Bright/z14/512 case adds **350**, for **3,150** measured
  requests, excluding warmups and the initial ten-sample pilot.
- Styles reused, canvas new per request. Warm source preloads the decoded tile;
  fresh source includes constructor/metadata, an empty decoded cache and disposal.
  OS file cache and SQLite pooling are not flushed. Host background activity,
  clock/power state and thermals are uncontrolled. This is not a cold-disk test.
- Both profiling callbacks enabled. Every returned image matches a nonuniform CPU
  reference exactly. Cache outcomes and actual CPU backend checked; every published
  PNG decoded and compared, expected file counts and no temp leftovers checked.
  No GPU timings, fallback-as-GPU results or tolerance changes.
- Existing sample provenance is unresolved; no new dataset or rendered image was
  committed. See [licensing policy](../test-data-licensing.md).

Run from repository root:

```powershell
dotnet run --project VectorTileRenderer.Benchmarks -c Release -- --request-timing . artifacts/request-timing-baseline 50
dotnet run --project VectorTileRenderer.Benchmarks -c Release -- --request-timing . artifacts/request-timing-repeat 50 bright-z14-8580-10646-512
```

Each run creates a unique output directory. Local deliverables:

- [Full matrix raw JSON](../../artifacts/request-timing-baseline/20261115-164701-83b9fd3b/results.json)
  and [CSV summary](../../artifacts/request-timing-baseline/20261115-164701-83b9fd3b/summary.csv).
- [Repeated heavy case JSON](../../artifacts/request-timing-repeat/20261115-165047-4c0a4d83/results.json)
  and [CSV summary](../../artifacts/request-timing-repeat/20261115-165047-4c0a4d83/summary.csv).
- Each directory also contains host metadata and local CPU reference images.
  Earlier pilot: artifacts/request-timing-pilot/20261115-164513-200b7ad5.

## External request results

Each cell is **median / p95 milliseconds**, nearest-rank percentiles. Warm/fresh
render means direct Render without PNG caching. Miss includes rendering, PNG
encoding and publication; hit loads a caller-owned bitmap. Hit + PNG also performs
the demo's subsequent encoding to a managed byte array. Provider/canvas creation
and disposal are included. Returned bitmap disposal, pixel validation and report
writing are excluded; neither GUI scheduling nor the entire Mapsui call is measured.

| Style / zoom / size | Render warm | Render fresh | Miss warm | Miss fresh | Hit bitmap | Hit + PNG |
|---|---:|---:|---:|---:|---:|---:|
| Basic / 12 / 256 | 30.08 / 43.71 | 40.67 / 75.73 | 37.03 / 54.29 | 48.28 / 72.01 | 1.03 / 1.37 | 7.24 / 8.53 |
| Basic / 12 / 512 | 29.56 / 37.38 | 34.49 / 44.27 | 48.54 / 55.51 | 58.16 / 72.93 | 2.56 / 2.75 | 20.01 / 21.92 |
| Basic / 14 / 256 | 30.47 / 45.42 | 68.91 / 85.63 | 42.23 / 54.79 | 78.47 / 106.12 | 1.00 / 1.12 | 7.17 / 8.05 |
| Basic / 14 / 512 | 37.71 / 51.84 | 79.27 / 102.24 | 60.97 / 76.40 | 100.15 / 117.59 | 2.63 / 2.97 | 20.36 / 22.07 |
| Bright / 12 / 256 | 58.44 / 75.96 | 65.68 / 86.60 | 67.79 / 92.52 | 74.60 / 117.76 | 1.06 / 1.53 | 7.25 / 8.81 |
| Bright / 12 / 512 | 55.39 / 67.63 | 64.42 / 74.55 | 78.31 / 88.38 | 87.74 / 95.49 | 2.68 / 3.00 | 21.18 / 22.60 |
| Bright / 14 / 256 | 44.06 / 57.71 | 95.37 / 104.58 | 61.46 / 72.88 | 100.99 / 118.12 | 1.12 / 1.31 | 9.10 / 9.81 |
| Bright / 14 / 512 | 56.90 / 164.90 | 116.26 / 406.91 | 94.12 / 209.27 | 143.51 / 436.38 | 3.23 / 5.58 | 31.72 / 66.55 |

The final full-matrix case had a noisy tail. Its fresh-process repeat was:

| Bright / 14 / 512 mode | Median ms | p95 ms |
|---|---:|---:|
| Render warm | 58.16 | 73.69 |
| Render fresh | 101.89 | 117.71 |
| Miss warm | 93.49 | 103.04 |
| Miss fresh | 131.16 | 151.55 |
| Hit bitmap | 3.14 | 3.48 |
| Miss warm + client PNG | 124.84 | 137.72 |
| Hit + client PNG | 31.44 | 32.59 |

The extreme first-run p95 is not a stable latency claim. GC counts are recorded,
but no event trace proves the cause of individual outliers; do not attribute them
solely to GC or to external activity. The repeat corroborates median encoding and
provider-reuse costs. No confidence intervals or broad machine-independent claims.

## Where render time goes

Full-matrix direct warm-render phase medians, milliseconds:

| Style / zoom / size | External total | Build | Style (inside build) | Geometry | Text only | Finish |
|---|---:|---:|---:|---:|---:|---:|
| Basic / 12 / 256 | 30.08 | 20.49 | 10.45 | 6.07 | 2.93 | 0.011 |
| Basic / 12 / 512 | 29.56 | 18.97 | 11.74 | 6.94 | 3.88 | 0.009 |
| Basic / 14 / 256 | 30.47 | 12.25 | 4.37 | 7.91 | 4.13 | 0.010 |
| Basic / 14 / 512 | 37.71 | 14.21 | 4.50 | 9.71 | 10.56 | 0.011 |
| Bright / 12 / 256 | 58.44 | 42.02 | 25.73 | 10.62 | 5.33 | 0.011 |
| Bright / 12 / 512 | 55.39 | 36.03 | 23.22 | 12.41 | 5.19 | 0.010 |
| Bright / 14 / 256 | 44.06 | 23.52 | 9.79 | 15.54 | 3.78 | 0.009 |
| Bright / 14 / 512 | 56.90 | 30.52 | 18.51 | 18.55 | 7.01 | 0.010 |

Build includes provider fetch/decode, validation/style evaluation, attributes and
localized geometry, plus visual-layer preparation. Fetch/style columns are nested;
phase medians need not sum to the median total. External totals also include setup,
cleanup, callbacks and overhead. No per-object allocation trace was collected.

Warm-miss encoding/publication medians ranged from **7.35–9.14 ms at 256 pixels**
and **18.74–29.76 ms at 512 pixels**; lookup was approximately **0.14–0.19 ms**.
This is not an isolated filesystem-write measurement: PNG compression and cleanup
are inside the same phase. Concurrency=1 cannot establish cache-lock contention.

Managed allocation medians: direct warm **12.00–19.89 MiB**, direct fresh
**21.52–50.07 MiB**, bitmap hit about **12 KiB**, hit plus PNG **105–393 KiB**.
Current-thread counters are guarded against thread changes; native allocations are
not included. Pixel verification allocates outside timing and can affect later GC.
No forced collection between samples. These runs do not establish a native-memory
plateau, first-start latency or distinct-tile navigation cache hit rates.

## Validation and investigation status

- Restore-enabled full Release solution/four-target build: zero warnings/errors.
- Regression suite: **104 passed, no skips**. Final case-filter addition also built.
- All full-matrix/repeat output and published-PNG pixel checks passed; no temp
  leftovers. No production renderer/host contracts changed.
- GitNexus index initially stale at 552177c; index-only refresh hit an EPERM registry
  write in the sandbox. Escalated retry succeeded with current baseline 8e53adf.
  Keyword query still reports missing FTS indexes. Exact versioned RenderCached
  context resolved; benchmark Main impact LOW/no graph callers, consistent with an
  executable entry point. Source verified the mode switch. detect-changes LOW/no
  affected flows but excludes the untracked new helper; not proof of no risk.

R-006 remains In progress: CPU repeated-tile baseline is now measured. Next define
the encoded-output experiment and validate exact pixels/cache behavior before
comparing old/new complete requests. After that, investigate build/decode allocations
and distinct-tile/concurrent navigation if needed. Existing GPU parity/hosting,
full bidi and cross-tile labels are unchanged. Raw files/images stay local; no
package bump, publication, commit or push in this measurement slice.
