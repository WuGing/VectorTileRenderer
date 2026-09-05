<!-- Illustrative record; IDs are examples, not live links. -->

## C-014: Cached rendering may outlive caller-owned bitmaps

Source: User
Status: Not investigated
Priority: High
Area: Resource Ownership, Caching
GitHub Issue:
Related concerns:
Related questions: Q-004

Summary:
I suspect asynchronous tile caching and native canvas resources have unclear lifetimes.

Why I suspect this:
- A caller may finish using a bitmap before a background cache write finishes.
- GPU resources need a clear host/thread owner.

What I want answered:
- Who owns the returned bitmap and the cache writer's image?
- When are the canvas, surface and graphics context released?

Potential impact:
- Failed cache writes or native memory growth.

Notes:
- Record reproduction steps and environment before treating this as a runtime defect.

Desired outcome:
- Explicit ownership and repeatable lifecycle tests.

Linked hypotheses:
- H-003
Linked findings:
- F-007
Linked refactor items:
- R-004
