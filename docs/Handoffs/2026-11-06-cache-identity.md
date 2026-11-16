# Source revision cache keys — 2026-11-06

Baseline 552177c, following [cache recovery](2026-11-06-cache-recovery.md).
Reproduced stale PNG reuse after changing a provider under the same style.
Added a RenderCached overload with a caller-supplied sourceRevision token;
the original signature and version-5 unversioned keys remain available.

[API definition, evidence, validation and limits](../02_Investigation/Cache-Identity-2026-11-06.md).
92 tests pass; full Release solution/four library targets build without warnings,
and NuGet pack passes. Dataset/revision isolation, persistent hits, old-signature/key
compatibility, concurrent writes and corruption recovery are covered.

F-006 Mitigated, R-005 In progress: callers must opt in and update tokens; storage
remains unbounded. Next design explicit caller-invoked cache maintenance with clear
directory ownership and filesystem safety tests before implementing eviction.
No version bump, commit, remote issue update or publication. Bidi/cross-tile labels
and existing GPU limitations remain open.
