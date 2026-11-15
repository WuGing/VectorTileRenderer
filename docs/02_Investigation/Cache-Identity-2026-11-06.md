# Caller-supplied source revision — 2026-11-06

Inspected baseline: `552177c374f19598a321581b785a35e1c66b94de`, following the
[corrupt-cache recovery slice](Cache-Recovery-2026-11-06.md). Windows x64,
.NET SDK 10.0.400. Changes remain local.

## Reproduction

A regression rendered a diagonal line, rebound the same style to a provider returning
a horizontal line, and requested identical coordinates/options in the same directory.
Before adding revision keys, the assertion that the output pixels should differ failed:
the second request returned the first provider's PNG. This is a reproduced stale-data
case under [F-006](Findings/F-006.md), rather than only a source-inspection concern.

## API and compatibility decision

[Renderer.RenderCached](../../VectorTileRenderer/Renderer.cs) retains its original
public signature as a forwarding wrapper. A new overload accepts a required
`string sourceRevision` after zoom and before optional output-size arguments.
Callers can supply `sourceRevision:` by name. No provider interface changes.

The token identifies the complete set of bound source datasets and their revisions;
callers must change it when any source data changes. It is opaque, case-sensitive,
and stable across provider instances/restarts. Null forwards to the exact original
version-5 key serialization and shortened hash. Any non-null token, including empty,
hashes a JSON envelope containing the original options and token with full SHA-256.
Versioned filenames thus use a separate namespace from legacy shortened hashes;
token text never becomes a path component. No dataset content is hashed automatically.

This is opt-in compatibility: existing consumers still need directory partitioning
or migration to a revision token. Neither object identity nor a file path provides
a stable content revision. A constant token without revision updates cannot prevent
stale images. Eviction is unchanged; switching revisions retains old files.

Usage and caller obligations are documented in the
[NuGet README](../../VectorTileRenderer/README.NUGET.md). No demo is assigned an
invented dataset revision; consuming apps must choose their own revision policy.

## Impact and validation

GitNexus status reported baseline 552177c current. Upstream impact rated RenderCached
HIGH (nine dependents), including Mapsui requests/prefetch and Gmap requests, matching
the source inspection in the preceding slice. The index does not cover these new
uncommitted overload/test symbols. Cumulative detect-changes reported critical with
24 flows and adjacent unchanged helper symbols; this is broad graph scope, not proof
that those callers break. Prior missing-FTS/registry limitations are documented in
the [preceding report](Cache-Recovery-2026-11-06.md).

Validation addresses the shared API risk:

- `dotnet test VectorTileRenderer.sln -c Release --no-restore --verbosity quiet`: **92/92 pass**.
- Regressions cover dataset changes, revision changes, null versus empty token,
  identical token hits with fresh Style/provider objects, old delegate signature,
  seeded version-5 cache hits, and null-token compatibility.
- Concurrent publication and empty/invalid PNG recovery run with both legacy and
  versioned keys, including eight requests and no leftover temporary files.
- `dotnet build VectorTileRenderer.sln -c Release --no-restore --verbosity quiet`:
  all projects/four library targets, zero warnings/errors.
- `dotnet pack VectorTileRenderer/VectorTileRenderer.csproj -c Release --no-build --no-restore -o artifacts/cache-revision-package --verbosity quiet`: passed.
- `git diff --check`: passed. No rendering algorithm/assets changed; no visual or
  GPU experiment claimed. No separately compiled old-consumer binary was executed;
  preservation of its method signature is source- and delegate-confirmed.

## Next bounded work

R-005 is partially implemented; F-006 is Mitigated, with legacy caller adoption and
unbounded storage still open. Next define an explicit, caller-invoked cache maintenance
operation before implementing eviction: directory ownership, byte/age limits,
temporary-file handling, concurrent reads/writes, and permission failure behavior.
Do not delete files from caller directories on ordinary rendering requests. Establish
synthetic filesystem acceptance cases before any cleanup implementation. Full bidi,
cross-tile placement, and GPU parity/hosting remain separate unresolved work.
