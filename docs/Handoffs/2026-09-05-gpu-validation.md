# Handoff — GPU experiment executed

Library baseline 7bc45e1; new isolated VectorTileRenderer.GpuValidation executable
and solution entry. Shipping renderer/demos unchanged. Read
docs/02_Investigation/Gpu-Validation-2026-09-05.md and its JSON summary first.

RTX 4070 Ti hosted WGL rendering and checked readback work. Auto negative probe
remains sticky; context abandonment returns a production bitmap despite failed
readback. Pumped asynchronous rendering stays on the host thread; an unpumped
provider is rejected by the harness guard before native calls. Three context
recreations pass. One dense 256px geometry case fails fixed pixel tolerances;
11 others pass. Exit 1 is retained for that failure; no tolerance relaxation.

Initial measurement: artifacts/gpu-validation-final/results.json and PNGs (ignored).
Thirty samples/backend/case plus five warmups, 11 eligible cases, alternating order.
No case reaches the predeclared 1.10x total speedup. CPU Render median is lower in
10 of 11 measured cases; decode and PNG costs matter. This is a reused-host single-thread
experiment, not production RenderCached, UI latency, cold disk or concurrent load.

Full Release solution passes with 0 warnings/errors; 40 NUnit tests pass; nupkg and
snupkg pack successfully. No production fixes, commits or GitHub writes. GitNexus
refreshed successfully but impact traversal remains partial (read-only pool error).

Next: explicit production readback failure and lifetime contracts (R-001/R-002),
host-scoped Auto policy, and failed image-case investigation. R-001 stays In progress
because its first experiment is complete but shipping integration/fixes are not.
Do not call the current GPU backend production-validated or broadly faster.

User follow-up requested a large local dataset. Added an optional MBTiles argument
and ran Colorado (248.69 MiB), largest compressed native tiles at zooms 10/12/14,
both reused and fresh SingleMbTilesSource modes. See Gpu-Colorado-2026-09-05.md and
its summary. Eight of nine distinct image cases pass (16/18 across cache modes);
the zoom-14 256px failure remains. No timing reaches 1.10x speedup; provider reuse
is much more material. CPU and GPU both show upside-down street labels. Raw results
and PNGs are in artifacts/gpu-validation-colorado. Other states were inventoried
but not rendered. Dataset and images remain local. Full solution, 40 tests and
packaging passed after the extension. Distinct-tile/cache-growth and concurrency
experiments are still pending.
