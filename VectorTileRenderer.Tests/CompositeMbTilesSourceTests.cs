using System.Security.Cryptography;
using NUnit.Framework;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Tests;

[TestFixture]
public class CompositeMbTilesSourceTests
{
    [Test]
    public void Constructor_RejectsAnEmptySourceList()
    {
        Assert.That(
            () => new CompositeMbTilesSource(Array.Empty<SingleMbTilesSource>()),
            Throws.ArgumentException.With.Message.Contains("At least one"));
    }

    [Test]
    public void PathConstructor_AggregatesCoverageAndZoomMetadata()
    {
        using var west = CreateWestFixture();
        using var east = CreateEastFixture();
        using var composite = new CompositeMbTilesSource([west.Path, east.Path]);

        Assert.Multiple(() =>
        {
            Assert.That(composite.Paths, Is.EqualTo(new[] { west.Path, east.Path }));
            Assert.That(composite.Sources, Has.Count.EqualTo(2));
            Assert.That(composite.MinZoom, Is.EqualTo(composite.Sources.Min(source => source.MinZoom)));
            Assert.That(composite.MaxZoom, Is.EqualTo(composite.Sources.Max(source => source.MaxZoom)));
            Assert.That(composite.Bounds.West, Is.EqualTo(composite.Sources.Min(source => source.Bounds.West)));
            Assert.That(composite.Bounds.East, Is.EqualTo(composite.Sources.Max(source => source.Bounds.East)));
            Assert.That(composite.Bounds.South, Is.EqualTo(composite.Sources.Min(source => source.Bounds.South)));
            Assert.That(composite.Bounds.North, Is.EqualTo(composite.Sources.Max(source => source.Bounds.North)));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void GetRawTile_RoutesToTheDatabaseCoveringTheRequestedRegion(bool useWestFixture)
    {
        using var west = CreateWestFixture();
        using var east = CreateEastFixture();
        var expectedFixture = useWestFixture ? west : east;
        using var expectedSource = new SingleMbTilesSource(expectedFixture.Path);
        using var composite = new CompositeMbTilesSource([west.Path, east.Path]);

        using var expected = expectedSource.GetRawTile(
            expectedFixture.TileX,
            expectedFixture.TileY,
            expectedFixture.Zoom);
        using var actual = composite.GetRawTile(
            expectedFixture.TileX,
            expectedFixture.TileY,
            expectedFixture.Zoom);

        Assert.Multiple(() =>
        {
            Assert.That(expected, Is.Not.Null);
            Assert.That(actual, Is.Not.Null);
            Assert.That(Hash(actual), Is.EqualTo(Hash(expected)));
        });
    }

    [Test]
    public void GetRawTile_FallsBackWhenHigherPriorityCoverageHasNoTile()
    {
        using var westFixture = CreateWestFixture();
        using var eastFixture = CreateEastFixture();
        var west = new SingleMbTilesSource(westFixture.Path);
        var east = new SingleMbTilesSource(eastFixture.Path);
        var world = new GlobalMercator.GeoExtent
        {
            West = -180,
            South = -85,
            East = 180,
            North = 85
        };
        using var composite = new CompositeMbTilesSource(
        [
            new MbTilesCoverage(east, 10) { Bounds = world, MinZoom = 0, MaxZoom = 22 },
            new MbTilesCoverage(west, 0) { Bounds = world, MinZoom = 0, MaxZoom = 22 }
        ]);

        using var expected = west.GetRawTile(westFixture.TileX, westFixture.TileY, westFixture.Zoom);
        using var actual = composite.GetRawTile(westFixture.TileX, westFixture.TileY, westFixture.Zoom);

        Assert.That(Hash(actual), Is.EqualTo(Hash(expected)));
    }

    [Test]
    public async Task GetVectorTile_UsesHigherPrioritySourceInsideOverlap()
    {
        using var lowerPriorityFixture = CreateOverlapFixture("lower-priority", featureId: 3);
        using var higherPriorityFixture = CreateOverlapFixture("higher-priority", featureId: 4);
        var lowerPriority = new SingleMbTilesSource(lowerPriorityFixture.Path);
        var higherPriority = new SingleMbTilesSource(higherPriorityFixture.Path);
        using var composite = new CompositeMbTilesSource(
        [
            new MbTilesCoverage(lowerPriority, 0),
            new MbTilesCoverage(higherPriority, 10)
        ]);

        var tile = await composite.GetVectorTile(
            higherPriorityFixture.TileX,
            higherPriorityFixture.TileY,
            higherPriorityFixture.Zoom);

        Assert.Multiple(() =>
        {
            Assert.That(tile, Is.Not.Null);
            Assert.That(tile.Layers, Has.Count.EqualTo(1));
            Assert.That(tile.Layers[0].Name, Is.EqualTo(higherPriorityFixture.Name));
        });
    }

    [Test]
    public void CandidateSelection_UsesPriorityThenNativeZoomThenSmallestCoverage()
    {
        using var westFixture = CreateWestFixture();
        using var eastFixture = CreateEastFixture();
        var west = new SingleMbTilesSource(westFixture.Path);
        var east = new SingleMbTilesSource(eastFixture.Path);
        var mercator = new GlobalMercator();
        var coordinate = mercator.LatLonToTile(
            westFixture.Latitude,
            westFixture.Longitude,
            westFixture.Zoom);
        var tileBounds = mercator.TileLatLonBounds(coordinate.X, coordinate.Y, westFixture.Zoom);
        var world = new GlobalMercator.GeoExtent
        {
            West = -180,
            South = -85,
            East = 180,
            North = 85
        };
        var highPriorityOverzoom = new MbTilesCoverage(east, 10)
        {
            Bounds = world,
            MinZoom = 0,
            MaxZoom = westFixture.Zoom - 1
        };
        var smallNativeCoverage = new MbTilesCoverage(west, 0)
        {
            Bounds = tileBounds,
            MinZoom = 0,
            MaxZoom = westFixture.Zoom
        };
        var largeNativeCoverage = new MbTilesCoverage(east, 0)
        {
            Bounds = world,
            MinZoom = 0,
            MaxZoom = westFixture.Zoom
        };
        using var composite = new CompositeMbTilesSource(
            [largeNativeCoverage, smallNativeCoverage, highPriorityOverzoom]);

        var candidates = composite.GetSourcesForTile(coordinate.X, coordinate.Y, westFixture.Zoom);

        Assert.That(candidates, Is.EqualTo(new[]
        {
            highPriorityOverzoom,
            smallNativeCoverage,
            largeNativeCoverage
        }));
    }

    [Test]
    public async Task GetVectorTile_PreservesSingleSourceOverzoomBehavior()
    {
        using var fixture = CreateWestFixture();
        using var source = new SingleMbTilesSource(fixture.Path);
        using var composite = new CompositeMbTilesSource(
            [new MbTilesCoverage(source)],
            disposeSources: false);
        var zoom = source.MaxZoom + 1;
        var coordinate = new GlobalMercator().LatLonToTile(fixture.Latitude, fixture.Longitude, zoom);

        var tile = await composite.GetVectorTile(coordinate.X, coordinate.Y, zoom);

        Assert.Multiple(() =>
        {
            Assert.That(tile, Is.Not.Null);
            Assert.That(tile.IsOverZoomed, Is.True);
            Assert.That(tile.Layers, Is.Not.Empty);
        });
    }

    [Test]
    public void Dispose_RejectsFurtherReadsButCanLeaveCallerOwnedSourcesOpen()
    {
        using var fixture = CreateWestFixture();
        using var source = new SingleMbTilesSource(fixture.Path);
        var composite = new CompositeMbTilesSource(
            [new MbTilesCoverage(source)],
            disposeSources: false);

        composite.Dispose();
        using var sourceTile = source.GetRawTile(fixture.TileX, fixture.TileY, fixture.Zoom);

        Assert.Multiple(() =>
        {
            Assert.That(
                () => composite.GetRawTile(fixture.TileX, fixture.TileY, fixture.Zoom),
                Throws.TypeOf<ObjectDisposedException>());
            Assert.That(sourceTile, Is.Not.Null);
        });
    }

    [Test]
    public async Task GetTile_ReturnsNull_WhenNoDatabaseCoversTheCoordinate()
    {
        using var west = CreateWestFixture();
        using var east = CreateEastFixture();
        using var composite = new CompositeMbTilesSource([west.Path, east.Path]);
        const int zoom = 10;
        var coordinate = new GlobalMercator().LatLonToTile(0, 0, zoom);

        using var tile = await composite.GetTile(coordinate.X, coordinate.Y, zoom);

        Assert.That(tile, Is.Null);
    }

    private static TestMbTilesFixture CreateWestFixture()
    {
        return TestMbTilesFixture.Create(
            "west-region",
            new GlobalMercator.GeoExtent
            {
                West = -112.5,
                South = 39.5,
                East = -111.5,
                North = 40.5
            },
            latitude: 40,
            longitude: -112,
            featureId: 1);
    }

    private static TestMbTilesFixture CreateEastFixture()
    {
        return TestMbTilesFixture.Create(
            "east-region",
            new GlobalMercator.GeoExtent
            {
                West = -111.5,
                South = 39.5,
                East = -110.5,
                North = 40.5
            },
            latitude: 40,
            longitude: -111,
            featureId: 2);
    }

    private static TestMbTilesFixture CreateOverlapFixture(string name, uint featureId)
    {
        return TestMbTilesFixture.Create(
            name,
            new GlobalMercator.GeoExtent
            {
                West = -112.25,
                South = 39.75,
                East = -111.75,
                North = 40.25
            },
            latitude: 40,
            longitude: -112,
            featureId: featureId);
    }

    private static byte[] Hash(Stream stream)
    {
        Assert.That(stream, Is.Not.Null);
        stream.Position = 0;
        return SHA256.HashData(stream);
    }
}
