# CPU workloads measured — 2026-11-15

Baseline 8e53adf. Added --request-timing mode to the existing benchmark executable;
normal BenchmarkDotNet path preserved. No production renderer changes.
[Results, scope, commands and raw-data links](../02_Investigation/Request-Workloads-2026-11-15.md).

Full matrix: Zurich heaviest native z12/z14 tiles, Basic/Bright, 256/512, seven
modes (warm/fresh direct render, warm/fresh cache miss, hit, miss/hit plus client
PNG encoding), five warmups and 50 measurements each. 2,800 main samples plus
350 for a fresh-process repeat of the noisy Bright/512 case. Exact CPU reference
pixels and all published PNGs verified. Full build and 104 tests pass.

Main costs: warm builds/style/allocations, fresh-provider decode, PNG output. Repeat
Bright/z14/512: warm render 58.16 ms, fresh 101.89; hit bitmap 3.14 versus 31.44
with client PNG. Caller PNG phase 28.14 ms; warm-miss encode/publication 29.57 ms.
CPU finish about 0.01 ms. Fresh render p95 changed from 406.91 to 117.71 ms on
repeat: record noise, no attribution without an event trace. This is not GPU data,
cold filesystem, whole-map traffic, UI-frame latency or an allocation-object profile.

Next bounded slice: define an encoded-output path for the byte-oriented Mapsui
consumer to avoid cache-hit bitmap decode/re-encoding and duplicate miss encoding.
Keep existing bitmap APIs/source keys/recovery/publication/ownership contracts;
prove equivalent output and compare complete requests before claiming improvement.
Preserve provider reuse. Build/decode allocation investigation can follow.

Raw artifacts are ignored locally; report is versionable. No commit/push/publication
in this slice. R-006 In progress; GPU parity, bidi and cross-tile work remain open.
