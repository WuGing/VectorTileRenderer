# Test-data licensing and provenance

This document records the repository policy for map test data. It is practical
project guidance, not legal advice.

## Data tiers

1. **Automated regression data:** NUnit creates tiny MBTiles databases at
   runtime from project-authored point geometry. These deterministic fixtures
   are the default for metadata, decoding, routing, overlap, fallback, and
   overzoom tests.
2. **Developer-local real-world data:** Put downloaded or organization-owned
   files in `tiles/real/`. Dataset files there are ignored by Git. Developers
   are responsible for selecting a source, following its license, and retaining
   required attribution.
3. **Repository sample assets:** A binary map asset may be committed only when
   its provenance, redistribution permission, and attribution requirements are
   documented.

## MapTiler data

As checked on 2026-09-01, MapTiler's OpenStreetMap download page offers its free
download for non-commercial personal projects and evaluation or educational
purposes. Its Server & Data terms separately limit third-party access and place
some redistribution scenarios under a custom agreement. Evaluation permission
therefore should not be treated as permission to publish the downloaded
MBTiles file in this repository.

- [MapTiler OpenStreetMap downloads](https://data.maptiler.com/downloads/tileset/osm/)
- [MapTiler Server & Data terms](https://www.maptiler.com/terms/server-data/)

Recheck the applicable terms at download time because vendors can change them.
The project does not require or endorse MapTiler; developers may use any source
whose terms cover their intended local testing.

## Open provenance task

The repository already contains `tiles/zurich.mbtiles`,
`tiles/islamabad.mbtiles`, related PBF files, and reference images. Their source
and redistribution permissions are not documented in the repository. Before a
public release, identify and record the source and license for each asset, add
any required attribution, or replace/remove assets whose redistribution rights
cannot be established. Automated tests no longer depend on those MBTiles files,
so this audit can be completed without weakening the regression suite.
