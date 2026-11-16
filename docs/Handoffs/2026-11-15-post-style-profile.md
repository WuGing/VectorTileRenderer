# Post-style complete profile checkpoint — 2026-11-15

`f79f882` plus pending performance work.
[Evidence](../02_Investigation/Post-Style-Profile-2026-11-15.md).

1,260 timed requests, all exact CPU pixels/publications pass. Eight reference PNG
hashes match. Two 150-request-per-phase traces, zero lost events, all 622 published
PNGs match. Heavy repeat medians fresh 84.95 ms, warm 62.43 ms, hit 2.38 ms.

Fresh mean 87.81 ms, preparation/style 33.9%, decode 30.8%, drawing 26.2%, PNG 8.1%.
Warm mean 62.14 ms, preparation/style 47.8%, drawing 40.1%, PNG 11.5%.
GetValue remains ~19% fresh / 24–26% warm inclusive samples, recursive filtering
~18% / 24%. Shares overlap. Native drawing and fresh decode remain significant.

Next isolate repeated filter-tree interpretation/literal resolution and feature/
layer preparation, then define a request-local evaluator experiment if justified.
Attribute and geometry caches already exist per feature/request. Preserve mutable
inputs, typed comparisons, missing attributes and exception/short-circuit order.
Other-preparation duration needs finer attribution before assigning it to copies.

Measurements/docs only. Benchmark build and runtime pixel checks pass. Prior
checkpoint passed 186 tests, restore-enabled solution build and packaging. Host
variation and observers limit latency comparisons. Actual GPU/UI/concurrent cases
remain separate. Work is uncommitted.
