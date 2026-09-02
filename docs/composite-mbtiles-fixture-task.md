# Composite MBTiles adjoining-region fixture task

The checked-in Zurich and Islamabad databases validate routing between separate
coverage areas. They cannot validate behavior at a shared boundary or inside an
overlap.

Prefer producing two project-authored vector MBTiles fixtures. If downloaded
real-world data is used instead, keep it under `tiles/real/` and do not commit it
unless its license expressly permits repository redistribution.

The desired fixtures have:

- geographically adjoining regions, plus a narrow intentional overlap;
- the same vector schema and a style already supported by this repository;
- accurate `bounds`, `minzoom`, and `maxzoom` metadata;
- at least one shared zoom level, preferably between zooms 10 and 14;
- one tile present in both files so priority can be demonstrated visibly;
- documented source, license, and generation commands (project-authored
  geometry is preferred for checked-in fixtures);
- cropped coverage to keep each checked-in fixture as small as practical.

Suggested destination names:

```text
tiles/composite/region-a.mbtiles
tiles/composite/region-b.mbtiles
```

Once available, add a static-demo view spanning the boundary and image
regressions for no gap, deterministic overlap priority, and overzoom behavior.

The NUnit suite already generates synthetic adjoining and overlapping MBTiles
databases at runtime to verify routing, fallback, priority, and overzoom. The
remaining task is a visually meaningful pair for seam inspection in the demo.
