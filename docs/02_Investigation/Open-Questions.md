# Open questions

## Q-005: Where is the repository license text?

Status: Open
Area: Documentation, Build / Packaging
Related concerns:
Related findings:

Question:
The root README links to LICENSE, but no matching license file was found in this
checkout. Should the original license text be restored with verified provenance?

Why it matters:
The project declares MIT in package metadata, but its README link is broken.

How to answer:
- Check repository history and intended license provenance; restore the correct
  text if appropriate. Do not invent attribution or treat package metadata as the
  missing file. This pre-existing link was discovered during documentation QA.

## Q-001: What GPU hosting contract and performance target should be supported?

Status: Open
Area: GPU / Hosting
Related concerns:
Related findings: F-001, F-007

Question:
Can the actual WPF/Mapsui host retain a native context on one render thread through asynchronous tile fetch, context loss, and shutdown? What end-to-end speedup justifies maintaining GPU mode?

Why it matters:
A successful context probe is insufficient to guarantee drawing or speed.

How to answer:
- Run the R-001 matrix and record actual backend, pixel comparison, readback status, context recreation and timings.

## Q-002: Should the product produce bitmap tiles or present a GPU map surface?

Status: Open
Area: Rendering
Related concerns:
Related findings: F-008

Question:
Is API-compatible offline bitmap rendering the priority, or interactive GPU presentation with shared labels across tiles?

Why it matters:
This choice determines whether to replace ICanvas or evaluate a full map engine.

How to answer:
- Compare the current bitmap API, a host surface prototype and a MapLibre Native spike using Backend-Investigation.md; document the decision before changing public APIs.

## Q-003: Who owns resources, cache identity and cancellation?

Status: Open
Area: Resource Ownership, Threading / Concurrency
Related concerns:
Related findings: F-002, F-006

Question:
Who disposes canvas/context/bitmap, waits for cache writes, versions source data, and cancels obsolete demo requests? Should profiling be scoped per renderer?

Why it matters:
Static state and fire-and-forget work cross caller lifetimes; instance conversion alone does not resolve these contracts.

How to answer:
- Trace RenderCached and both demo providers with GitNexus context plus source; test immediate bitmap disposal, repeated renders, missing tiles and host shutdown.

## Q-004: What style and label compatibility is promised?

Status: Open
Area: Style Evaluation, Text / Symbols
Related concerns:
Related findings: F-003, F-004, F-005

Question:
Which expressions, sprites, circles, scripts, text placement, DPI/zoom combinations and tile-edge behaviors must work?

Why it matters:
Missing point rendering, interpolation and shaping gaps need bounded acceptance criteria.

How to answer:
- Create a capability matrix and synthetic fixtures, including non-square output and scale factors; replay applicable upstream examples with legally usable data.
