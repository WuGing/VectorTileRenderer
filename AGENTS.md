# Repository Guidelines

## Project Structure & Module Organization

`VectorTileRenderer/` contains the reusable library. Rendering entry points and
canvas backends live at its root; tile-provider abstractions and implementations
belong in `VectorTileRenderer/Sources/`. The library targets `netstandard2.0`,
`net7.0`, `net8.0`, and `net10.0`.

The solution also contains three Windows desktop examples:
`Static.Demo.WPF/`, `Mapsui.Demo.WPF/`, and `Gmap.Demo.WinForms/`. Shared sample
data is kept in `tiles/`, Mapbox-style JSON and fonts in `styles/`, and reference
renders in `images/`. Do not casually replace these binary assets; keep additions
small and explain their provenance.

`VectorTileRenderer.Tests/` contains NUnit regression tests, including synthetic
MBTiles fixtures. `VectorTileRenderer.Benchmarks/` contains isolated
BenchmarkDotNet experiments. Both belong to `VectorTileRenderer.sln`.

## Build, Test, and Development Commands

Use .NET SDK 10, as configured in CI:

```powershell
dotnet restore VectorTileRenderer.sln
dotnet build VectorTileRenderer.sln -c Release --no-restore
dotnet build VectorTileRenderer/VectorTileRenderer.csproj -c Release
dotnet pack VectorTileRenderer/VectorTileRenderer.csproj -c Release -o artifacts
dotnet run --project Static.Demo.WPF/Static.Demo.WPF.csproj
```

The full solution and demos require Windows. Building the library alone is the
portable validation path (native dependencies still require runtime validation). Run tests with
`dotnet test VectorTileRenderer.sln -c Release`.

## Coding Style & Naming Conventions

Follow the existing C# style: four-space indentation, file-scoped namespaces,
Allman braces, and one primary type per file. Use `PascalCase` for public types,
members, and files; use `camelCase` for parameters, locals, and private fields.
Keep source namespaces under `WuGing.VectorTileRenderer`, with providers under
`.Sources`. The solution uses `LangVersion=latest` and warnings as errors. The library has nullable analysis
disabled; check each project's settings and handle nullable values deliberately. Avoid adding suppressions
without justification. Run `dotnet format VectorTileRenderer.sln` before
submitting broad formatting changes, and keep formatting-only edits separate.

## Testing Guidelines

For renderer changes, cover deterministic geometry, coordinate conversion, style
evaluation, and missing/corrupt tile behavior. Name tests after observable
behavior, for example `GetTile_ReturnsNull_WhenCoordinateIsOutsideCoverage`.
Include a focused regression test with every bug fix. For visual changes, run the
relevant demo against checked-in sample tiles and attach before/after screenshots
to the pull request.

## Commit & Pull Request Guidelines

History favors short, imperative summaries such as `Update package version` or
`Fix namespace in demos`. Keep each commit focused and avoid unrelated asset or
format churn. Pull requests should explain intent, list validation commands,
identify affected target frameworks/backends, link relevant issues, and include
screenshots for rendering or UI changes. Ensure both library packaging and the
Windows solution build pass before merge.

## GitNexus for code evaluation

Use the applicable GitNexus exploring, debugging, impact-analysis, refactoring,
or PR-review skill when investigating code or evaluating a change. Check index
freshness first. Prefer `npx --no-install gitnexus analyze --index-only` when
refreshing so indexing does not inject duplicate contributor instruction files.

Use query for execution flows and context for callers/callees; inspect the source
to verify graph results. Before changing public contracts, shared state or symbols,
run upstream impact analysis and describe affected projects and tests. Use the
available detect_changes operation before commit/PR review to check scope. Consult
CLI --help for supported commands when MCP tools are unavailable.

If indexing/querying fails or the graph is incomplete, record the exact limitation
and use targeted rg/source inspection and tests. An empty result is not proof of
no callers. A high-risk result requires explaining the risk and a validation plan;
it does not by itself require another permission request for authorized work.
Documentation-only edits need link/schema checks, not symbol impact analysis.

## Investigation documentation and GitHub

AGENTS.md is the shared instruction source; .github/copilot-instructions.md points
here. Read docs/README.md for the canonical structure, schemas and example prompts.
Then read relevant user priorities/concerns, the review plan, current architecture
and latest handoff. Preserve user wording under docs/00_User-Input; put investigator
evidence/findings under docs/02_Investigation and proposals under docs/03_Target-State.
Continue existing IDs, keep unknown fields blank, and link source symbols, inspected
commit/date, validation results and full GitHub URLs. Distinguish comments and
hypotheses from source-confirmed behavior and reproduced runtime defects.

Use WuGing/VectorTileRenderer for delivery issues and AliFlux/VectorTileRenderer
only as historical upstream input. Follow docs/02_Investigation/GitHub-Tracking.md:
check open/closed duplicates, exclude PRs from issue inventories, preserve remote
edits, and record returned URLs after successful authorized publication. Git pull
synchronizes commits, not issue records. Never commit credentials.

For GPU work, prove actual GPU surface creation and successful pixel readback;
a requested backend or CPU fallback is not a GPU pass. Compare deterministic CPU
pixels before timings, record hardware/host/runtime and cold/warm cache state,
and measure complete requests. Label proposed replacement APIs and define them
before implementation. Respect docs/test-data-licensing.md for visual fixtures.

Example prompts:
- Trace RenderCached ownership with GitNexus and document evidence for F-002.
- Assess ICanvas callers and output compatibility before proposing a new backend.
- Validate GPU activation/pixels, then benchmark complete CPU/GPU tile requests.
- Import GitHub issue status and classify applicability without changing remote issues.
- Resume docs/02_Investigation/Review-Plan.md and the latest docs/Handoffs entry.

Use .github/prompts for reusable investigation, resume, wrap-up and issue-sync tasks.
