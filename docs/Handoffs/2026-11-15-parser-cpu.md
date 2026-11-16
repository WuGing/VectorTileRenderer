# Parser profiling — 2026-11-15

Baseline f79f882 with pending decode cleanup. Two independent 1,000-request captures
per parser/conversion/fresh-decode phase, both zero lost events.
[Evidence and measurement limits](../02_Investigation/Parser-Cpu-2026-11-15.md).

GC-poll frames occupy 63–66% of parser thread samples. Among remaining samples,
attributes 40–43%, protobuf 32–38%, geometry 14–15%. Collector measures thread wall
time and cannot establish exact CPU percentages. Next inspect dependency attribute
and protobuf materialization, then compare a bounded candidate with exact model
fingerprints and unprofiled complete requests. Profile tooling reuses allocation
markers, collects no allocation ticks, and preserves earlier workload entry points.

Release benchmark build passed. Both new entry points completed twice with all
three windows and resolved sample stacks. All 120 regression tests passed.
Work remains uncommitted.
