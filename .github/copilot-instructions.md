# Copilot instructions for VectorTileRenderer

Follow [AGENTS.md](../AGENTS.md) for repository structure, build/test requirements,
GitNexus use, investigation evidence and change validation. It is the shared
contributor instruction source; do not duplicate it here.

For investigation artifacts, read [docs/README.md](../docs/README.md), relevant
user priorities/concerns, the review plan and latest handoff before inspecting
code. Preserve user-authored notes; put analysis in docs/02_Investigation and
proposals in docs/03_Target-State. Use the documented field order, IDs, evidence
levels and GitHub tracking workflow.

This repository contains VectorTileRenderer, three Windows demos, NUnit tests
and BenchmarkDotNet experiments. Renderer output is SKBitmap; GPU mode is
experimental. Validate the actual backend and pixels before claiming GPU support
or speed. Do not import architecture assumptions from unrelated projects.

Example prompts:
- “Trace Renderer.RenderCached with GitNexus and source; investigate bitmap ownership and cache invalidation.”
- “Assess the impact of changing ICanvas, including all demos and tests, before proposing an API.”
- “Validate F-001 with a host-owned GPU context and CPU pixel reference.”
- “Run dotnet test VectorTileRenderer.Tests/VectorTileRenderer.Tests.csproj -c Release.”
- “Triage GitHub reports against the current fork and update docs/02_Investigation/GitHub-Tracking.md.”

Reusable task prompts live in [.github/prompts](prompts/).
