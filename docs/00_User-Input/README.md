# User input for VectorTileRenderer

Use this folder to capture what you want investigated: rendering correctness, GPU behavior, speed, tile coverage, style compatibility, cache behavior, desktop integration, or missing features. The canonical folder layout, schemas, and status values are in [docs/README.md](../README.md).

## Where to write

- `My-Notes.md`: informal observations and ideas.
- `Priority-Areas.md`: what matters most and why.
- `Concern-Backlog.md`: structured C-### concerns.
- `Hypotheses.md`: your suspected explanations; investigator hypotheses belong in `../02_Investigation/Hypotheses.md`.
- `Evidence.md`: logs or observations you provide, including environment and reproduction steps.
- `Initiative-Backlog.md`: larger outcomes you would like to pursue.
- [Examples](Examples/): reusable record shapes. Example IDs and situations are illustrative.

Your notes are inputs, not automatically confirmed findings. Assistants preserve their meaning and place analysis in `02_Investigation/`. A blank backlog does not mean there are no known issues; see the [current audit](../02_Investigation/Review-Plan.md).

## Example concern

```md
## C-001: Is GPU rendering actually active and useful?

Source: User
Status: Not investigated
Priority: High
Area: GPU / Hosting, Rendering
GitHub Issue:
Related concerns:
Related questions: Q-001

Summary:
I cannot recall testing whether the GPU path works or improves tile rendering.

Why I suspect this:
- Selecting Auto can fall back to CPU.

What I want answered:
- Which host/thread owns the graphics context?
- Do GPU pixels match the CPU reference?
- Does end-to-end tile latency improve?

Potential impact:
- Misleading backend selection or wasted optimization effort.

Notes:
- Record GPU, driver, runtime, tile coordinates, style, and cache state.

Desired outcome:
- A reproducible correctness and performance result.

Linked hypotheses:
Linked findings:
Linked refactor items:
```

This is an example, not an automatically filed concern.

## Using GitHub

Track this fork at [WuGing/VectorTileRenderer](https://github.com/WuGing/VectorTileRenderer/issues). Historical reports belong to [AliFlux/VectorTileRenderer](https://github.com/AliFlux/VectorTileRenderer/issues); do not treat their numbers or statuses as this fork's.

Use `GitHub Issue: https://github.com/WuGing/VectorTileRenderer/issues/NUMBER` once an issue exists. Leave it blank beforehand. A concern does not need a GitHub ticket. Ask an assistant to investigate first or explicitly ask it to publish a reviewed issue. Include expected/actual behavior, reproduction data, environment, related local IDs, and acceptance criteria. See [connection and synchronization details](../02_Investigation/GitHub-Tracking.md).

Useful prompts:
- “Investigate my GPU concern and update docs with confirmed facts and remaining questions.”
- “Triage current GitHub issues against this checkout without changing their remote status.”
- “Create GitHub issues from the reviewed refactor plan, checking for duplicates and recording the returned URLs.”
