# Release PR checkpoint — 0.1.1-preview.7

Date: 2026-11-15
Repository: https://github.com/WuGing/VectorTileRenderer
Branch: codex/release-0.1.1-preview.7
Base: master
Pull request:

## Scope

The PR includes the existing local commits `03a6a70`, `8e53adf` and `f79f882`,
covering SDK Source Link restore repair, cache hardening/profiling and direct PNG
output. It also delivers the subsequent parser, encoding, style/preparation and
native path/paint optimization sequence, compatibility tests and investigations.
Package version and curated release notes advance to 0.1.1-preview.7.

Production retains standard protobuf deserialization and streaming gzip. Direct
renderer-feature/geometry conversion removes intermediate models. The rejected
direct-append protobuf reader is reachable only through benchmark experiments and
linked test source. Full geometry-path caching was discarded. Canvas path reuse
owns one scratch path per drawing session with deterministic cleanup.

## Performance evidence

[Direct cumulative comparisons](../02_Investigation/Post-Submission-Profile-2026-11-15.md)
against the saved pre-parser build measured 42–43% lower fresh request medians and
44–50% lower reused-provider medians, with 40%/46% less managed allocation.
That comparison already starts after direct PNG output and precedes the final
direct geometry decoder. The [final decoder comparison](../02_Investigation/Fresh-Decode-2026-11-15.md)
separately measured another 9–10% lower fresh medians and 2.36 MiB less allocation.
These are scoped CPU tile-request measurements from alternating runs.

Exact CPU pixels match the eight-case Zurich Basic/Bright z12/z14 256/512 matrix.
Four typed decode snapshots match. Tests cover numeric/culture/filter behavior,
geometry bits and malformed inputs, mutable style edits, cache contracts, and native
path/paint/bitmap ownership. Raw traces/results and local hand-test binaries stay
under ignored artifacts, with summaries and reproduction commands in the reports.

## Release validation

Restore-enabled solution build, all 205 Release tests, 41 focused Debug geometry/
path tests, all-target packaging and package metadata/license contents pass.
Package manifest version and curated notes match the project, and README and
third-party notices are included with all four library targets. The hand-test build
is republished with the release version and a matching BUILD-INFO.txt. The latest
requested human visual/interaction pass is pending.
The desktop-tool launch attempt in the prior checkpoint timed out on approval.
Actual GPU, concurrent requests and GUI frame-rate measurements are separate.

GitNexus committed index matches pre-release HEAD. Final staged detection reports
critical aggregate scope, 76 files / 120 symbols / 123 processes. Newly staged
files have limited symbol coverage in that index. Source inspection and the
solution/package/pixel/ownership checks cover the delivery scope. R-006 remains
In progress for additional workloads and ongoing profiling.
