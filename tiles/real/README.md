# Real-world MBTiles validation data

Place locally acquired real-world `.mbtiles` files in this directory when you
want to exercise the demos or perform larger integration and performance tests.
Everything in this directory except this README is ignored by Git.

No particular data vendor is required. Obtain files from a source of your
choice, and verify that your license permits your intended use. Keep any
required attribution with your local test setup. Do not commit downloaded data
unless its license expressly permits redistribution through this repository.

The automated test suite does not require these files. It creates tiny
synthetic MBTiles databases at runtime from geometry authored by this project.
This keeps regression tests deterministic and avoids redistributing third-party
map data.

See [`docs/test-data-licensing.md`](../../docs/test-data-licensing.md) for the
repository policy and the open provenance audit for older sample assets.

For composite-source seam testing, see
[`docs/composite-mbtiles-fixture-task.md`](../../docs/composite-mbtiles-fixture-task.md).
