# Complete request profile — 2026-11-15

Baseline f79f882 plus pending direct conversion/performance work.
[Evidence and limits](../02_Investigation/Complete-Request-Profile-2026-11-15.md).

Eight-case matrix plus heavy repeat gives 1,260 validated timings. Heavy fresh
direct-PNG median 116–118 ms; repeat mean 122.96 ms. PNG encode/publication 25.7%,
fetch/decode 23.6%, style/other preparation 23.3%, geometry/text 26.3%, other ~1%.
Reused-provider miss mean 92.40 ms, PNG 34.1%, preparation/style 37.4%.
Hits median ~3.14 ms, dominated by full decode validation.

Two 150-request-per-phase captures, zero lost events. Sampled encoder ~32% fresh /
37–38% warm, publication ~2%, filter evaluation and path drawing remain substantial.
Inclusive samples overlap and describe thread wall time. Timings use observers;
trace workloads disable them. Every trace publication and final output passes
pixel checks outside marked windows. Native codec internals remain unresolved.

148 tests/full Release build pass. New `--request-cpu-workload` and
`--request-cpu-report` entry points reuse the trace analyzer with a distinct marker
provider. Next define a PNG encoder experiment with request time/encoded size/exact
pixel checks, then consider filter/preparation or path construction leads.
Work remains uncommitted; no version bump or publication.
