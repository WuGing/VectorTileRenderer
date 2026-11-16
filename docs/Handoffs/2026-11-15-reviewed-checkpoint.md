# Reviewed cache/profiling checkpoint — 2026-11-15

Reviewed cumulative changes from baseline
`552177c374f19598a321581b785a35e1c66b94de` before local checkpoint commits.
[User feedback](../00_User-Input/Manual-Validation-2026-11-15.md) is a broad positive
manual result; no per-checklist or GPU/performance pass is inferred.

## Review and validation

Reviewed renderer overload compatibility, cache recovery/publication, pruning
ownership/limits, profiling callbacks/timing boundaries, tests, and Mapsui revision/
maintenance/provider lifetime integration. No blocking correctness finding in this
scope. This is a self-review, not an independent review or release approval.

Validation on Windows x64, SDK 10.0.400:

- `dotnet build VectorTileRenderer.sln -c Release` passed with restore, all four
  library targets and Windows projects, zero warnings/errors.
- `dotnet test VectorTileRenderer.Tests/VectorTileRenderer.Tests.csproj -c Release --no-restore`:
  104 passed, no skips.
- `dotnet pack VectorTileRenderer/VectorTileRenderer.csproj -c Release --no-build --no-restore -o artifacts/checkpoint-2026-11-15`:
  library and symbol packages generated locally.
- `git diff --check` and changed-document relative-link checks passed.

GitNexus status reports baseline 552177c current; cumulative uncommitted additions
are not indexed. detect-changes reports critical/77 flows and includes adjacent
unchanged helpers. Earlier source-verified impact includes Mapsui, Gmap and harness
consumers. Missing FTS and incomplete/new-symbol coverage remain documented limits;
source inspection and regressions are authoritative. No symbol-absence claim.

## Remaining limits and next slice

No GPU runtime test or new visual comparison in this review. Existing GPU parity,
host/thread integration, full bidi and coordinated cross-tile placement remain open.
Pruning is explicit, last-write-based and process-local; limits can be exceeded by
subsequent writes. Revision policy and maintenance invocation remain caller-owned.
Profiling preserves legacy semantics and host queues/consumer PNG encoding remain
outside cache-request boundaries.

Next R-006 slice: add a repeatable CPU workload to the existing validation/benchmark
tooling. Use checked-in real MBTiles with deterministic coordinates, warm versus
fresh providers, cache hit versus forced miss, complete external request timings,
phase medians/p95, allocation measurements and host/runtime/cache-state metadata.
Verify output correctness and actual backend before timing; keep verified-GPU
measurements a separate hardware run with unchanged parity gates. Do not optimize
until results identify an actionable cost. Interactive rolling overlay data is not
the baseline. No package version bump, push, PR or publication in this checkpoint.
