# Manual validation build unblock — 2026-11-12

User's default `dotnet run --project Mapsui.Demo.WPF/Mapsui.Demo.WPF.csproj`
failed during restore with NU1902 for Microsoft.Build.Tasks.Git 8.0.0,
[GHSA-23fw-v26w-5fgq](https://github.com/advisories/GHSA-23fw-v26w-5fgq).
The attempted `dotnet update` command does not exist.

Inspected baseline 552177c plus cumulative working-tree changes. Assets confirmed
Microsoft.SourceLink.GitHub 8.0.0 brought in the affected build-only package.
Removed that one explicit reference from the
[library project](../../VectorTileRenderer/VectorTileRenderer.csproj). This repository
requires SDK 10; [official Source Link documentation](https://github.com/dotnet/sourcelink#using-source-link-in-net-projects)
confirms GitHub support is bundled from SDK 8 onward, and an explicit package
overrides that bundled implementation. No vulnerability suppression or audit change.
Keep SDK installations patched; local SDK is 10.0.400.

Validation on Windows x64:

- `dotnet restore VectorTileRenderer.sln`: passed; old Git/SourceLink packages absent
  from the regenerated assets.
- `dotnet build VectorTileRenderer.sln -c Release`: restore-enabled, all projects
  and four library targets passed, zero warnings/errors.
- `dotnet build Mapsui.Demo.WPF/Mapsui.Demo.WPF.csproj`: default Debug and restore
  passed, matching the user's build configuration, zero warnings/errors.
- `dotnet test VectorTileRenderer.Tests/VectorTileRenderer.Tests.csproj -c Release --no-restore`:
  104 passed, no skips.
- Release Source Link JSON still maps repository sources to the GitHub commit.
- Release library and symbol package generation passed to ignored
  artifacts/sourcelink-sdk-package.

GitNexus status remains baseline-current; package query returned no flows with
`FTS indexes missing — keyword search degraded`. Package assets and the project
reference are the authoritative dependency evidence; no symbol/public-contract edit.

Previous --no-restore validation did not exercise the user's restore/audit blocker.
Include restore-enabled builds in this checkpoint. Resume the
[manual checklist](../Manual-Validation.md); the GUI was not relaunched in this fix.
No commit, version bump or publication.
