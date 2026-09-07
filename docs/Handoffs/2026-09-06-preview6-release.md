# Preview.6 packaging handoff - 2026-09-06

Updated VectorTileRenderer.csproj to 0.1.1-preview.6 with curated notes for road
orientation/fit/placement, point-label bounds, cache invalidation, Auto fallback
and GPU readback. GPU and broader text limitations remain explicit.

The published preview.5 package downloaded from NuGet.org contains literal
`&#10;` sequences in releaseNotes. Its NuGet.org page has a Release Notes tab,
but displays the escaped sequences in one paragraph. The publish workflow caused
this by overriding the project notes with encoded git-log output.

The workflow now exports project notes for GitHub releases and packs the project
metadata directly. It verifies version, exact packed release notes and a packaged
README with the matching release version. README.NUGET.md also displays the notes
on the main package page.

Validation: preview.6 pack succeeded for four target frameworks; the workflow's
metadata verification script passed locally. Independently checked real newlines
and all four assemblies in the nupkg. Git diff whitespace check passed. GitHub's
hosted workflow and NuGet.org preview.6 display have not run yet: no publication,
tag or commit was performed. Local package: artifacts/release-preview6.

For future releases, update Version, PackageReleaseNotes and README.NUGET.md
release notes together. Tags must match the project version, as enforced by CI.

Published page inspected:
https://www.nuget.org/packages/WuGing.VectorTileRenderer/0.1.1-preview.5
