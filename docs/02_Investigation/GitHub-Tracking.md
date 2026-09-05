# GitHub connection and issue tracking

Verified 2026-09-05.

## Repository and authentication

- Origin: `https://github.com/WuGing/VectorTileRenderer.git`; upstream: `https://github.com/AliFlux/VectorTileRenderer.git`.
- `git ls-remote origin HEAD` returned `a4f8ca87dbb7870cbae9ca5d61881b0f3e0cedcc`, matching local HEAD at audit time. This verifies connectivity and matching HEAD, not every remote branch.
- The connected GitHub account is WuGing; connector reads succeeded.
- GitHub CLI (`gh`) is not installed. Existing Git Credential Manager credentials also authenticated REST reads successfully; repository permissions report admin, maintain, push, triage and pull.
- Initial repository setting was `has_issues: false`, explaining the empty issue listing. See the final verification below for the setting change.
- Never print credentials or put them in docs, scripts, issue bodies or command arguments. Use the connected GitHub tools or an authenticated credential manager. Do not add a token to the repository.

## Synchronization procedure

1. Read live repository state and list issues with `state=all`; follow pagination and exclude PR entries. Search titles, related IDs and reproduction before creating duplicates.
2. Treat origin as the delivery tracker. Use upstream as historical input; do not silently copy reports or change upstream state.
3. Keep local investigation findings and proposals even before an issue exists. For an authorized publication, use the title, objective, evidence, scope and validation in the corresponding R-### record as a concrete draft.
4. Record the returned full issue URL in the finding/refactor record and a tracking table. Do not invent a number or mark a local draft published before a successful response.
5. Before editing a remote issue, re-read it and merge intentionally; preserve others' changes. Pull status updates with a checked date and note local/remote discrepancies.
6. Git fetch/pull/push synchronize commits; they do not synchronize Markdown records with Issues. There is no background bidirectional sync configured. Use explicit pull/import and publish/update tasks.
7. On future sessions, revalidate authentication and repository permissions. If gh is desired, install it separately and authenticate interactively; the connector and existing credential manager already provide working access.

## Upstream open issue snapshot

The connector returned 18 open issue records (excluding PRs) on 2026-09-05. These are historical reports, not confirmed defects in this fork. This is the returned open-issue snapshot, not an audit of closed history or every comment/attachment.

| Upstream issue | Applicability to this fork |
|---|---|
| [#35: Cannot zoom into mbtiles taken from osmlab.github.io](https://github.com/AliFlux/VectorTileRenderer/issues/35) | Unreproduced here; verify tile schema/style pairing and label orientation. |
| [#34: How to use the lib with Vector tiles or SHP files?](https://github.com/AliFlux/VectorTileRenderer/issues/34) | Feature request; current providers consume PBF/MBTiles/raster, not a direct SHP pipeline. |
| [#32: Loading a style cause duplicate attribute values found error](https://github.com/AliFlux/VectorTileRenderer/issues/32) | Historical parser error; replay a minimal fixture against current mapbox-vector-tile 5.3.0 before accepting. |
| [#29: Support custom style with expression in future fully?](https://github.com/AliFlux/VectorTileRenderer/issues/29) | Relevant compatibility request; partial style support and F-005 require an operator matrix. |
| [#27: At some zoom levels some tiles are not rendered](https://github.com/AliFlux/VectorTileRenderer/issues/27) | Unreproduced; modern single/composite routing tests do not prove this dataset/zoom case. |
| [#26: Caching of tiles?](https://github.com/AliFlux/VectorTileRenderer/issues/26) | Relevant performance proposal; measure H-001 and design bounded caching in R-005. |
| [#25: Solved issue with labels](https://github.com/AliFlux/VectorTileRenderer/issues/25) | Historical text solution proposal; inspect script/font approach, do not assume merged. F-003. |
| [#24: Why does the renderer have to use BitmapSource ? ](https://github.com/AliFlux/VectorTileRenderer/issues/24) | Original BitmapSource return complaint is superseded here by SKBitmap; engine coupling remains F-008. |
| [#21: Replacement for SkiaSharp? Known issues with Vector Tile Rendering especially labels](https://github.com/AliFlux/VectorTileRenderer/issues/21) | Relevant research question; see backend comparison. No evidence here that replacing Skia alone fixes labels. |
| [#20: Overzooming openmaptiles osm vector tiles ](https://github.com/AliFlux/VectorTileRenderer/issues/20) | Historical overzoom report; replay against SingleMbTilesSource; existing tests are not this reproduction. |
| [#19: How to using OpenGL in SkiaGL?](https://github.com/AliFlux/VectorTileRenderer/issues/19) | Relevant GPU request; experimental implementation exists but host/runtime validation remains F-001. |
| [#18: MBTiles database](https://github.com/AliFlux/VectorTileRenderer/issues/18) | Usage/documentation report; current README has SingleMbTilesSource and GMap examples. |
| [#17: Load file MbTiles big size(~1.6GB)](https://github.com/AliFlux/VectorTileRenderer/issues/17) | Historical large-file/startup/shutdown report; current source differs; add licensed repro and lifecycle measurements. |
| [#14: Active developed](https://github.com/AliFlux/VectorTileRenderer/issues/14) | Project activity/contribution discussion; not a runtime defect. |
| [#13: Unity support](https://github.com/AliFlux/VectorTileRenderer/issues/13) | Unity integration request; no Unity demo in this solution; separately validate native packages/platforms. |
| [#10: Interpolated styles not supported](https://github.com/AliFlux/VectorTileRenderer/issues/10) | Historical interpolation report; current InterpolateValues exists, so do not label all interpolation absent. F-005. |
| [#7: Street Names](https://github.com/AliFlux/VectorTileRenderer/issues/7) | Historical street-label report; reproduce with matched style/schema/fonts. F-003. |
| [#2: .NET Standard support](https://github.com/AliFlux/VectorTileRenderer/issues/2) | Original .NET Standard request is implemented in current csproj; native runtime portability still needs platform validation. |

## Local publication backlog

R-001 through R-007 in [Refactor Plan](../03_Target-State/Refactor-Plan.md) are issue-ready local proposals. No issues or comments were posted during this audit. The user requested working issue access; publication of specific work items remains a separate explicit action.

## Final connection verification

Enabled Issues through the authenticated repository API and re-read the setting: `has_issues: true`. Admin/push/triage/pull permissions remain available. No issue-write smoke test was posted; creation/update capability is supported by enabled Issues and the authenticated permissions, not a performed issue mutation.


The final all-state REST listing contained two closed pull requests (#1 and #2), and zero actual issues after excluding PRs. The connector open-issue listing was also empty. No pagination was needed for this fork's two-record response.
