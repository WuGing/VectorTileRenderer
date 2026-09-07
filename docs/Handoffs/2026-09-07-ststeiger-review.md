# External fork review - 2026-09-07

Compared our 3a263ea with ststeiger a5a7749 (2021-09-08).
[Full evaluation and pinned source links](../02_Investigation/Ststeiger-Fork-2026-09-07.md).

Useful lessons: invariant numeric parsing, real glyph coverage and renderer-owned
canvas lifetime. Fork road text is disabled; mixed-script fallback truncates words;
GPU initialization and server disk-cache operations are commented out. No wholesale
merge recommended. No fork execution or performance comparison claimed.

Reproduced F-010 in our uncached color parser: rgba alpha differs under de-DE and
throws under fr-FR. Recommend the bounded culture fix next, then F-002 ownership
and F-003 fallback/shaping. Production code remains unchanged. Probe and sparse
external checkout are ignored under artifacts. No remote changes made.
