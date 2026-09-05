# Review plan — 2026-09-05

Scope: code comments and unfinished branches, README known issues, GPU correctness/performance assumptions, backend alternatives, existing docs, and GitHub tracking. Source baseline: `a4f8ca87dbb7870cbae9ca5d61881b0f3e0cedcc`. This is an investigation and documentation change, not a renderer fix.

## Results and priority

| Priority | Record | Result / next work |
|---|---|---|
| High | [F-001](Findings/F-001.md) | GPU hosting, fallback state, and readback validation gaps; run R-001 |
| High | [F-002](Findings/F-002.md) | Native resource lifecycle and background cache bitmap ownership need explicit contracts |
| High | [F-003](Findings/F-003.md) | Text fallback truncation and incomplete symbol/path placement |
| Medium | [F-004](Findings/F-004.md) | Point/icon drawing is a stub |
| Medium | [F-005](Findings/F-005.md) | Color interpolation and unsupported style cases |
| Medium | [F-006](Findings/F-006.md) | Disk cache has no eviction and does not identify source data |
| Medium | [F-007](Findings/F-007.md) | Profiling cannot isolate GPU finish/readback and uses a caller hint |
| Medium | [F-008](Findings/F-008.md) | Backend abstraction returns SKBitmap |
| Medium | Existing fixture task | Build visually meaningful adjoining-region fixture with documented provenance |

See [Evidence](Evidence.md) for all TODO markers and related comments, [Hypotheses](Hypotheses.md) for unmeasured performance risks, [Open Questions](Open-Questions.md), [Refactor Plan](../03_Target-State/Refactor-Plan.md), and [backend comparison](../03_Target-State/Backend-Investigation.md).

## Method and limits

Searched authored C# across the library, demos, tests and benchmarks for TODO/FIXME/HACK/XXX, NotImplementedException, question/performance/bug comments, and read surrounding implementations. Also reviewed README, existing docs and upstream issue bodies. Generated designer files and vendored/binary assets are not actionable TODO records.

GitNexus was stale at 00bb07a; refreshed it to the current checkout. Its context lookup found SkiaGpuCanvas, but query reported missing FTS indexes and returned no flows. Source inspection is the basis for conclusions; graph absence is not evidence. The graph even listed inherited methods as overrides, so source remains the authority.

No GPU host was launched, no pixel parity experiment or benchmark was run, and no historical upstream reproduction was replayed. Findings describe source-confirmed behavior; runtime impact still needs the stated validation. No production code or binary assets changed.

## Next sequence

1. R-001: prove GPU activation, thread ownership, successful readback and pixel parity.
2. R-002: define native resource and cache-writer ownership with regression cases.
3. R-003/R-004: validate text/symbol and style gaps with small synthetic fixtures.
4. R-005/R-006: measure and improve cache policy and end-to-end instrumentation.
5. Only then choose a backend spike using R-007 and Q-002.

