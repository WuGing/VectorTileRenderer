# Corrupt PNG cache recovery — 2026-11-06

Inspected baseline: `552177c374f19598a321581b785a35e1c66b94de`; changes validated locally on Windows x64, .NET SDK 10.0.400.

## Reproduced behavior and implementation

[Renderer.RenderCached](../../VectorTileRenderer/Renderer.cs) returned the result of
SKBitmap.Decode immediately, including null for an empty or invalid PNG. Subsequent
requests kept loading the same broken entry. This was reproduced by three failing
regression cases before changing production code.

Cache reads now return only successfully decoded bitmaps. Undecodable entries are
deleted under the same process-local lock used for publication, before rendering
outside that lock. Existing atomic publication repairs the missing entry. Read/delete
IO and permission failures fall through to rendering; publication remains best effort.
Rendering exceptions still propagate. There is no public signature or cache-key change.

[RendererTests](../../VectorTileRenderer.Tests/RendererTests.cs) cover empty files,
invalid bytes, eight concurrent recovery requests, decoding the repaired file, a
subsequent cache hit with identical pixels and no rendering, and temporary-file cleanup.

## Validation and impact

- Before fix: all three new cases failed; after fix: full solution suite 84/84 passed.
- `dotnet test VectorTileRenderer.sln -c Release --no-restore --verbosity quiet` passed.
- `dotnet build VectorTileRenderer.sln -c Release --no-restore --verbosity quiet` passed with zero warnings/errors, including all four library targets.
- `dotnet pack VectorTileRenderer/VectorTileRenderer.csproj -c Release --no-build --no-restore -o artifacts/cache-recovery-package --verbosity quiet` passed.
- `git diff --check` passed.

GitNexus status initially reported index commit 3a263ea versus current 552177c.
Refresh with `analyze --index-only` failed writing the external registry:
`EPERM: operation not permitted, open 'C:\Users\jdssu\.gitnexus\registry.json'`.
Query reported `FTS indexes missing — keyword search degraded` and no flows.
Context and upstream impact still returned RenderCached callers; source inspection
confirmed Mapsui request/prefetch and Gmap request paths. Impact rated HIGH with nine
dependents; detect-changes rated cumulative scope critical (18 flows), also mapping
unchanged adjacent test helpers. Treat this as incomplete graph evidence. The actual
diff is one cache-read block and regression tests; no host or GPU operations changed.

Full solution tests/build/package address the shared entry point risk. No demo visual
comparison or GPU runtime test was needed for this cache-only change. Cross-process
file replacement races and permission-denied behavior were not runtime-tested;
the existing lock coordinates only callers in the same process.

## Refined next slice: source-aware cache identity (R-005)

Source identity and eviction in [F-006](Findings/F-006.md) remain open. Tackle source
identity separately from eviction: reproduce two providers bound to identical style
JSON sharing one cache directory, then define an optional caller-supplied stable
dataset/revision token before implementing it. Preserve existing source compatibility
and document that callers must change the token when data changes. Object identity
is unsuitable for persistent keys; path alone does not identify a dataset revision.

Acceptance: distinct tokens cannot reuse each other's tile, the same token hits the
cache, changed revisions miss, existing calls compile, and concurrent atomic
publication/corruption recovery tests pass. Choose the public API after upstream
impact analysis. Full bidi and coordinated cross-tile labels remain separate work
under F-003; the paused alternative backend and GPU parity limits are unchanged.
