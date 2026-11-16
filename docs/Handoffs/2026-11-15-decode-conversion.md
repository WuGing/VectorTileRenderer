# Decode conversion cleanup — 2026-11-15

Baseline f79f882. Split parser-only/conversion-only allocation windows and added
exact decoded-model snapshot tooling. Parser 20.31 MiB/request; adapter 9.98 MiB.
Adapter now initializes its owned containers once with known capacities, using an
internal feature constructor. Public parameterless construction/fields/ownership
remain intact; no parser, dependency, geometry-math or renderer replacement.

[Evidence, fingerprints, raw links and timing limits](../02_Investigation/Decode-Conversion-2026-11-15.md).
Conversion allocation 9.98 → 7.67 MiB (23.2% less); full fresh decode 30.58 → 28.27
MiB (7.6% less), parser unchanged. Complete fresh direct-PNG miss 122.33 → 120.04 ms
median with noisier after-run p95: no material latency win claimed. Warm/hit costs
remain effectively unchanged. Both allocation traces report zero lost events.

120 tests passed; new synthetic typed attributes/duplicate keys/multipart geometry/
default constructor and ownership cases. All four decoded fixture fingerprints
match exact attribute types/values and coordinate bits; focused reference PNG is
byte-identical and measured outputs/publications match. Full Release build/all four
library targets and package pass. No new UI/GPU run or native-memory claim.

Next review/checkpoint. For further speed, CPU-profile parser/build/style before
another allocation-only cleanup or a dependency replacement. Existing GPU parity/
hosting, bidi and cross-tile limits unchanged. Work remains uncommitted; no version
bump, push or publication.
