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

There is currently no test project. Add new automated tests under a clearly named
project such as `VectorTileRenderer.Tests/` and include it in
`VectorTileRenderer.sln`.

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
portable validation path. Once tests exist, run them with
`dotnet test VectorTileRenderer.sln -c Release`.

## Coding Style & Naming Conventions

Follow the existing C# style: four-space indentation, file-scoped namespaces,
Allman braces, and one primary type per file. Use `PascalCase` for public types,
members, and files; use `camelCase` for parameters, locals, and private fields.
Keep source namespaces under `WuGing.VectorTileRenderer`, with providers under
`.Sources`. The solution uses `LangVersion=latest` and has nullable analysis
disabled, so handle nullable values deliberately and avoid adding suppressions
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
