# Post-drawing profile checkpoint — 2026-11-15

Baseline f79f882 plus pending drawing/performance changes.
[Evidence and limits](../02_Investigation/Post-Path-Profile-2026-11-15.md).

1,260 timed requests, exact pixels/publications pass; eight reference PNGs match.
Two 150-request-per-phase traces, zero lost events. Heavy fresh median 94.78 ms,
warm 69.63 ms, hit 2.76 ms in repeat; tails remain noisy.

Fresh mean 105.81 ms, preparation/style 34.09 ms (32.2%), decode 31.03 ms (29.3%),
drawing 30.71 ms (29.0%), PNG 8.61 ms (8.1%). Warm mean 80.79 ms, preparation/style
46.5%, drawing 40.8%, PNG 12.0%. Preparation leads combined timers; fresh costs
remain close. Style.GetValue ~20% fresh / 24–26% warm inclusive samples, recursive
filter validation ~18% / 22–23%. Inclusive percentages overlap.

Next inspect filter/value evaluation and layer preparation, isolating repeated
per-feature work that can be resolved per layer/request. Preserve mutable style,
typed comparisons, missing attributes and zoom semantics; require exact pixels and
complete-request validation. Native drawing and fresh decode remain substantial.

Measurements/docs only in this slice. Previous checkpoint passed 173 Release tests,
15 Debug path tests/full build/pack. Work remains uncommitted, no publication.
