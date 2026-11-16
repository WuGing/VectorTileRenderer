# Post-preparation profile checkpoint — 2026-11-15

`f79f882` plus pending performance work.
[Evidence and limits](../02_Investigation/Post-Preparation-Profile-2026-11-15.md).

1,260 timed requests pass exact CPU pixels/publications, eight reference hashes
match. Two 150-request-per-phase traces, zero lost events, all 622 publications
match. Heavy repeat medians fresh 78.38 ms, warm 53.99 ms, hit 2.38 ms.

Observer-free drawing-entry union is ~42% fresh / 53% warm, strongest common
next target. Line entry ~18% / 23%, polygons ~12% / 14%, text ~12% / 16%.
Path construction ~6–7% / 7–8%, within drawing entries. Filter/value/style union
~17% / 23%, ParseStyle <1% approximately. Inclusive shares overlap.

Instrumented repeat stages remain close. Fresh drawing 31.2%, decode 30.1%,
preparation/style 28.4%, PNG 9.3%. Warm preparation/style 45.0%, drawing 41.4%,
PNG 13.1%. Preparation observers add overhead and may affect ranking. Host
variation is substantial, so these runs supply no additional paired speedup claim.

Next isolate line/polygon path construction versus native draw/submission. Paths
already use batched construction and per-canvas paint reuse. Request-local path
reuse for repeated geometry needs actual repetition/lifetime measurements and exact
pixels, clipping/order/mutation checks. Fresh decode remains a closely sized lead.

Measurements/docs only. Benchmark build and runtime checks pass. Prior checkpoint
passed 190 tests, restore-enabled solution build and all-target pack. Actual GPU,
UI, concurrent and native-memory workloads remain separate. Changes uncommitted.
