# Explicit cache maintenance — 2026-11-07

Baseline `552177c374f19598a321581b785a35e1c66b94de`; continues the local
[revision-key work](Cache-Identity-2026-11-06.md). Windows x64, SDK 10.0.400.

## Defined and implemented API

[TileCache.Prune](../../VectorTileRenderer/TileCache.cs) accepts a caller-owned
directory, nonnegative maxBytes and optional nonnegative maxAge. It returns a
[TileCachePruneResult](../../VectorTileRenderer/TileCachePruneResult.cs) containing
DeletedFiles, DeletedBytes, RemainingBytes and FailedFiles. It runs synchronously
only when explicitly invoked; rendering never triggers cleanup.

Age means elapsed UTC time since last write, not LRU. Expired files are removed
first; remaining size is reduced oldest-written first with ordinal path tie-breaking.
Zero bytes removes all eligible tiles. Only immediate files matching renderer names
(coordinates/zoom and a 12- or 64-character hex hash, PNG extension) are candidates.
This includes previous rendering/source revisions. No content decoding is required.
Temporary files, unrelated names, subdirectories and file reparse points are excluded.
The directory and ancestors are checked for reparse points and rejected when linked.
Only use dedicated rendered-tile directories: naming alone cannot prove ownership.

Missing directories return an empty result without creating them. Invalid arguments
throw before deletion. Directory traversal/enumeration failures propagate; per-file
IO/permission failures increment FailedFiles and processing continues. Byte counts
reflect observed eligible metadata; unreadable metadata cannot contribute a known
size. Locked files can leave the result above maxBytes. No automatic retry or scheduler.

The existing Renderer cache lock is now internal so pruning can coordinate with
cache reads and atomic publication. Rendering/encoding remain outside it; temporary
files are ignored, and publication after a prune can exceed the cap. Schedule this
explicit operation away from latency-sensitive requests; it scans/sorts under the
process-wide lock. This is a best-effort maintenance pass, not a continuously enforced
quota. External processes/path replacement are not coordinated and must be controlled
by the directory owner. Reparse checks are not an adversarial filesystem sandbox.

## Validation

[TileCacheTests](../../VectorTileRenderer.Tests/TileCacheTests.cs) exercise oldest-first
byte eviction for both filename lengths, age eviction below budget, preserving temp/
foreign/nested files, locked-file failure accounting, missing directory behavior,
invalid limits, directory link rejection and file link target preservation. The lock
test is Windows-specific; symbolic link tests may skip if privileges are unavailable.
On this host all tests ran, including symbolic link creation.

[RendererTests](../../VectorTileRenderer.Tests/RendererTests.cs) additionally run
16 concurrent render/prune tasks, check caller-owned bitmaps after deletion, and
verify final publication remains decodable with exact pixel equality. Existing
revision isolation/corruption/concurrent publication tests remain in the full suite.

- `dotnet test VectorTileRenderer.sln -c Release --no-restore --verbosity quiet`:
  **100 passed, 0 failed, 0 skipped**.
- `dotnet build VectorTileRenderer.sln -c Release --no-restore --verbosity quiet`:
  all projects/four library targets, zero warnings/errors.
- `dotnet pack VectorTileRenderer/VectorTileRenderer.csproj -c Release --no-build --no-restore -o artifacts/cache-maintenance-package --verbosity quiet`: passed.
- `git diff --check`: passed. Documentation links checked.

GitNexus status reported 552177c up to date, but new untracked maintenance types
are not indexed. Upstream impact for cacheLock returned `Target 'cacheLock' not found`;
context still located RenderCached callers (Mapsui request/prefetch, Gmap requests).
Source inspection verifies the shared lock. Cumulative detect-changes reported
critical, 24 flows, including prior revision work and unchanged adjacent test helpers.
The graph is incomplete, so targeted filesystem/concurrency tests and full builds
are the validation basis. No native rendering, GPU or demo visual change is claimed.

## State and next work

R-005's recovery, opt-in revision identity and explicit bounded maintenance are now
implemented. F-006 remains Mitigated: consuming apps must supply revisions and invoke
maintenance; temporary files and external-process races remain outside this API.
No package version bump, remote issue publication or commit.

Next move to R-006: first audit existing profiling boundaries and reproduce what
TotalMs includes on cache hits, misses, finish/readback and encoding. Define metrics
before adding them, preserve existing callbacks and measure complete requests. No
optimization or backend replacement is justified solely by the cache changes.
