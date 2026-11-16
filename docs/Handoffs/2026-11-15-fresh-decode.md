# Fresh-decode and hand-test checkpoint — 2026-11-15

`f79f882` plus pending performance work.
[Evidence and reproduction](../02_Investigation/Fresh-Decode-2026-11-15.md).

Direct geometry-command conversion removes intermediate coordinates/segment
descriptors and writes normalized renderer point lists. Fresh complete requests
measure 9–10% faster, approximately 2.36 MiB fewer managed bytes per heavy tile.
SQL/metadata attribution is small on warmed local files. Standard protobuf and
streaming gzip remain in use. A malformed zero-count ClosePath that stalls the
reference decoder now fails fast. Other bounded malformed cases and exact bits
are checked against the dependency.

205 Release tests, 20 Debug geometry/parser tests, restore-enabled solution build,
all-target packaging and Mapsui publish pass. Four typed snapshots and eight CPU
reference PNG hashes match. All 960 matrix and 720 final paired requests pass
exact pixels/publications. Native/GPU/concurrent/UI timings are separately scoped.

Requested current hand-test build is under `artifacts/manual-testing-current`.
BUILD-INFO.txt records hash
`934E88F56239431E7962115595CCFE58F2667AD470EFD06EFC7E95DA4C32F2CE`.
Executable, dependencies, Zurich data/styles and 48 fonts are present. Desktop-tool
launch approval timed out, and no demo window was observed afterward. User can
launch the executable directly. [Checklist](../Manual-Validation.md).

Next receive manual feedback and profile current requests before selecting another
slice. Work is uncommitted, R-006 remains In progress.
