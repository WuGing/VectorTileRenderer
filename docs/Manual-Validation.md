# Manual validation: Mapsui demo

## Current hand-test build — 0.1.1-preview.7

Open [Mapsui.Demo.WPF.exe](../artifacts/manual-testing-current/Mapsui.Demo.WPF.exe)
from the published `artifacts/manual-testing-current` folder. It includes the
current direct geometry decoder and earlier performance work, with Zurich data,
styles and 48 fonts. Keep the accompanying files/folders together. It uses the
installed .NET Desktop Runtime 10 on Windows x64.

[Build information](../artifacts/manual-testing-current/BUILD-INFO.txt) records the
validated renderer hash. Release build, 205 tests, 20 Debug geometry/parser tests,
packaging and publish pass. Exact pixels match all eight cases and typed snapshots
match all four fixtures. Automatic launch through the desktop tool timed out
waiting for app approval, and a subsequent window inventory showed no demo window.
Startup and interaction checks are for this manual pass.

```powershell
& "C:\Source\VectorTileRenderer\artifacts\manual-testing-current\Mapsui.Demo.WPF.exe"
```

The demo is ready for a human visual/interaction pass. The window title is
**VectorTileRenderer — manual validation**. It starts with the checked-in Zurich
dataset and Basic style. If it is not running, from the repository root use:

```powershell
dotnet run --project Mapsui.Demo.WPF/Mapsui.Demo.WPF.csproj -c Release --no-build
```

## What to check

1. **Map quality and interaction:** pan and zoom around the covered region. Try
   Basic and Bright styles. Look for missing/stuck tiles, label corruption, obvious
   seams and stalls. Labels near edges may be omitted; coordinated cross-tile labels
   and complete mixed-direction text remain unresolved.
2. **Disk reuse:** let tiles settle and note the bottom overlay counters. Click
   **Reload map** without moving. The same view should return, with more disk hits.
   Reload creates a new map layer to bypass its in-memory tile cache. Counters are
   cumulative for the application session; compare before/after values.
3. **Revision invalidation:** click **New revision**. The number in the top status
   should increase and the same view should produce fresh misses, with unchanged
   map content. It changes the cache identity, not the dataset itself. Reload again
   to check hits for that revision.
4. **Clear and recover:** after requests settle, click **Clear rendered cache**.
   The top status should report removed files/remaining bytes/failures. Current
   on-screen tiles can remain visible. Click **Reload map** to force requests;
   fresh misses should restore the same imagery. Deletion affects only this demo's
   dedicated validation cache. New renders can repopulate it during maintenance.
5. **Bounded cleanup:** click **Trim cache (512 MiB / 30 days)**. A small fresh cache
   should remove nothing and report zero failures. Byte/age eviction itself is
   covered by synthetic tests; this step checks UI responsiveness and reporting.
6. **Restart:** close and reopen the demo. The revision should be preserved, and
   previously cached views should produce disk hits. Pan/zoom again and confirm
   the map remains responsive. Revision 1 was left by the automated smoke check.

For any failure, record the style, revision, approximate location/zoom, action,
overlay/status text and a screenshot. Distinguish a render error from a view outside
the dataset's coverage. Cache misses with missing tile output are counted as misses.

## Reading the overlay

- Disk hits/misses are session counters. Median/p95 cover the latest 200 cache
  requests, potentially mixing hits and misses; they are interactive diagnostics.
- Last request shows lookup, render and encode/write timings. Last render shows
  actual CPU/GPU status, geometry, text-only and finish times. A cache hit shows
  DiskCache; the last-render line can refer to an earlier request. Finish can round
  to 0.0 ms at display precision.
- Request timings cover RenderCachedPng, including miss encoding/publication.
  Display scheduling and canvas creation are outside those request measurements.
  The overlay supplies interactive diagnostics. Auto without a suitable host GPU context normally uses CPU;
  CPU output is not proof of GPU support. Existing GPU parity limits remain.

## Cache policy and scope

The dedicated directory is under the built demo output at
`tile-cache/manual-validation`. Its full path is shown in the window. Existing demo
cache files elsewhere are not used or cleared. The small manual-revision.txt file
is preserved by pruning and stores the manual revision across restarts.

The demo combines the absolute dataset path, length, last-write UTC ticks and manual
revision into its token. This is a sample metadata policy, not a content digest;
use New revision if source contents change without changing metadata. Fonts are
resolved from the built output's absolute styles/fonts path. Provider retirement
waits for active requests before disposing database resources. Previously disabled
neighbor-prefetch code was removed from this validation demo to keep request counts
focused on visible requests.

## Checks already completed

2026-11-15 PNG output follow-up: Mapsui now calls RenderCachedPng and returns its
bytes directly. The map and cache controls are unchanged. Cache hits still perform
full decode validation, but no longer re-encode; misses encode only once. The
profiler now also exposes OutputFormat and HasOutput for byte-oriented consumers.

2026-11-15: [User feedback](00_User-Input/Manual-Validation-2026-11-15.md) reports
that the demo seems to work and authorizes proceeding. This records a positive
manual checkpoint, not individual checklist/GPU/performance results.

2026-11-12 build unblock: removed the obsolete explicit Microsoft.SourceLink.GitHub
8.0.0 reference, which pulled in vulnerable Microsoft.Build.Tasks.Git 8.0.0 and
caused NU1902 during restore. Source Link is supplied by the required .NET 10 SDK.
Restore-enabled full Release and default Debug demo builds pass; 104 tests pass.
See the [build-fix handoff](Handoffs/2026-11-12-sourcelink-build.md). No audit warning
was suppressed. Keep the installed .NET SDK patched as well.

2026-11-07: 104 regression tests passed; full Release solution/four library targets
built with zero warnings/errors. A live UI smoke check confirmed rendered map output,
reload (+32 hits, no added misses) and revision change (+32 misses, revision 0 → 1).
The observed requests used CPU. Startup initially timed out at UI automation approval;
subsequent window access succeeded. Revision 1 persisted after reopening the final
build. Cache deletion controls and visual restart checks remain for the manual pass;
underlying deletion/concurrency/link
behavior is already covered by tests. Final provider-retirement adjustment compiled
cleanly; it was added after the reload/revision smoke checks.

See [the handoff](Handoffs/2026-11-07-manual-validation.md) for integration evidence.
