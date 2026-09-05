# VectorTileRenderer investigation documentation

Start with [the review plan](02_Investigation/Review-Plan.md), [the evidence inventory](02_Investigation/Evidence.md), and [the GPU/backend investigation](03_Target-State/Backend-Investigation.md). This documentation records the audit of commit `a4f8ca87dbb7870cbae9ca5d61881b0f3e0cedcc` on 2026-09-05.

## Folder ownership

| Folder | Purpose |
|---|---|
| `00_Project/` | Stable project scope and constraints |
| `00_User-Input/` | User concerns, priorities, and hypotheses; inputs, not proof |
| `01_Architecture/` | Evidence-backed current structure |
| `02_Investigation/` | Investigator evidence, hypotheses, findings, open questions, GitHub snapshots |
| `03_Target-State/` | Proposed changes and validation plans |
| `Handoffs/` | Dated resume notes with remaining work |

Existing [fixture task](composite-mbtiles-fixture-task.md) and [data licensing policy](test-data-licensing.md) remain authoritative for their subjects. Link them instead of duplicating their requirements. Create folders only when there is useful content.

## Record schema

The [examples](00_User-Input/Examples/) define field order and section names. Their IDs are illustrative, not reserved. Use one finding per file under `02_Investigation/Findings/`; hypotheses and questions can share index files. Preserve unknown fields as blank. All records may include `GitHub Issue:` with a full URL; use it in place of the imported Azure/Sentry fields.

| Record | Prefix | Fields, in order |
|---|---|---|
| Concern | C-### | Source, Status, Priority, Area, GitHub Issue, Related concerns, Related questions; Summary, Why I suspect this, What I want answered, Potential impact, Notes, Desired outcome, Linked hypotheses, Linked findings, Linked refactor items |
| Hypothesis | H-### | Status, Priority, Area, Related concerns, Related findings, Confidence; Statement, Why this is suspected, What still needs to be verified, Evidence so far, Next validation step |
| Finding | F-### | Status, Priority, Area, Related concerns, Related hypotheses, GitHub Issue, Confidence; Summary, Conclusion, Evidence, Impact, Scope, Recommendation, Suggested refactor items, Open questions, Notes |
| Refactor item | R-### | Status, Priority, Area, Related findings, GitHub Issue, Owner, Target milestone; Objective, Implementation outline, Risks, Validation |
| Question | Q-### | Status, Area, Related concerns, Related findings; Question, Why it matters, How to answer |
| Decision | D-### | Status, Date, Related findings, Related refactor items; Context, Options, Decision, Consequences, Validation |

Use existing sequences before allocating IDs. Cross-link Concern → Hypothesis → Finding → Refactor → Decision when such records exist; do not manufacture user concerns to fill links. Use repository-relative paths and symbols, line numbers when helpful, inspection date and commit, and commands/results for runtime evidence.

## Status and evidence rules

- Concerns: Not investigated, Queued, In review, Partially validated, Confirmed, Disproven, Deferred, Closed.
- Hypotheses: Open, Testing, Supported, Rejected, Superseded.
- Findings: Confirmed, Needs follow-up, Accepted, Mitigated, Closed.
- Refactors: Proposed, Approved, Planned, In progress, Implemented, Validated, Deferred, Cancelled.
- Questions: Open, Answered, Deferred. Decisions: Proposed, Accepted, Superseded.
- Priorities: Critical, High, Medium, Low, Nice-to-have. Confidence: Low, Medium, High.
- Areas: Rendering, GPU / Hosting, Text / Symbols, Style Evaluation, Tile Sources, Caching, Threading / Concurrency, Resource Ownership, Desktop Integration, Testing, Build / Packaging, Documentation.
- A source-confirmed behavior is not a reproduced runtime defect. State that distinction explicitly. A TODO or historical upstream issue is a lead, not proof.
- Measure performance before naming a bottleneck. Record hardware, OS, runtime, backend actually used, workload, cold/warm caches, allocations, and end-to-end timing.
- Source and reproducible tests outrank stale notes or incomplete GitNexus graphs. Never infer a missing caller or feature solely from an empty graph result.
- Keep user notes intact; put assistant analysis in `02_Investigation/`. Update the review plan and a dated handoff at completion.

## GitHub workflow

See [GitHub tracking](02_Investigation/GitHub-Tracking.md). Local IDs identify investigation records; GitHub numbers identify delivery work. Keep full URLs so fork and upstream issue numbers cannot be confused. Search open and closed issues before creating one. Import reports with date, state, source, and a local applicability verdict. Publish issues only when requested; record the returned URL in both the local finding and tracking table. Re-read the remote body before an update and preserve others' edits. Issue closure requires implementation and validation evidence, not merely a finished investigation.

## Example prompts

- “Audit TODOs and known issues in VectorTileRenderer; use GitNexus and source evidence, and update the review plan and findings.”
- “Investigate F-001 using a real host-owned OpenGL context. Prove the actual backend and pixel correctness before comparing CPU/GPU timings.”
- “Measure cold and warm tile rendering with the existing BenchmarkDotNet project; separate decode, style, drawing, readback, encoding, and cache contention.”
- “Validate upstream issue #29 against this fork and create a minimal style-expression regression before proposing changes.”
- “Publish the reviewed R-001 issue draft to WuGing/VectorTileRenderer after checking for duplicates, then record its URL.”
- “Resume from the latest handoff and identify the next unverified claim; do not implement a backend replacement yet.”

