# Post-PNG profile checkpoint — 2026-11-15

Baseline f79f882 plus pending performance changes.
[Evidence and limits](../02_Investigation/Post-Png-Profile-2026-11-15.md).

1,260 timed requests and two 150-per-phase stack captures, zero lost events, exact
pixel/publication checks pass. Matrix has substantial host-time variation. Focused
heavy Bright 512 fresh median 99.40 ms, warm 67.81 ms, hit 2.56 ms.

Fresh mean 103.17 ms, drawing 35.01 ms (33.9%), preparation/style 29.62 ms (28.7%),
fetch/decode 29.56 ms (28.7%), PNG 7.89 ms (7.6%). Warm mean 72.13 ms, drawing 47.3%,
preparation/style 40.8%, PNG 11.2%. Drawing leads combined stages; fresh fetch/decode
is the largest individual timer by a small margin.

Line/polygon sample entries total roughly 29% fresh / 38% warm. Path construction
is a descendant cost (~8% / 9–10%). Style.GetValue/filter work remains substantial.
Next investigate path construction/reuse versus native drawing, with exact pixels,
clipping/ownership checks and complete-request comparison. Keep style/filter as
a separate candidate. Native internals and GC-worker CPU remain unresolved.

This slice changes measurements/docs only. Previous checkpoint passed 158 tests/
full build/pack. Work remains uncommitted; no version bump or publication.
