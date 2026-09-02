using NUnit.Framework;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Tests;

[TestFixture]
public class MbTilesSourceTests
{
    [Test]
    public void Constructor_LoadsMetadataFromSampleDatabase()
    {
        using var fixture = CreateFixture();
        using var source = new SingleMbTilesSource(fixture.Path);

        Assert.Multiple(() =>
        {
            Assert.That(source.Bounds.West, Is.EqualTo(fixture.Bounds.West));
            Assert.That(source.Bounds.South, Is.EqualTo(fixture.Bounds.South));
            Assert.That(source.Bounds.East, Is.EqualTo(fixture.Bounds.East));
            Assert.That(source.Bounds.North, Is.EqualTo(fixture.Bounds.North));
            Assert.That(source.MinZoom, Is.EqualTo(fixture.Zoom));
            Assert.That(source.MaxZoom, Is.EqualTo(fixture.Zoom));
            Assert.That(source.Name, Is.EqualTo(fixture.Name));
        });
    }

    [Test]
    public async Task GetVectorTile_CachesDecodedTile()
    {
        using var fixture = CreateFixture();
        using var source = new SingleMbTilesSource(fixture.Path);

        var first = await source.GetVectorTile(fixture.TileX, fixture.TileY, fixture.Zoom);
        var second = await source.GetVectorTile(fixture.TileX, fixture.TileY, fixture.Zoom);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Null);
            Assert.That(first.Layers, Has.Count.EqualTo(1));
            Assert.That(first.Layers[0].Name, Is.EqualTo(fixture.Name));
            Assert.That(first.Layers[0].Features, Has.Count.EqualTo(1));
            Assert.That(first.Layers[0].Features[0].GeometryType, Is.EqualTo("Point"));
            Assert.That(second, Is.SameAs(first));
        });
    }

    [Test]
    public void GetRawTile_ReturnsNull_WhenCoordinateDoesNotExist()
    {
        using var fixture = CreateFixture();
        using var source = new SingleMbTilesSource(fixture.Path);

        using var tile = source.GetRawTile(-1, -1, source.MinZoom);

        Assert.That(tile, Is.Null);
    }

    [Test]
    public void Constructor_WrapsDatabaseErrorsWithSourceContext()
    {
        var missingPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"missing-{Guid.NewGuid():N}.mbtiles");

        var exception = Assert.Throws<InvalidOperationException>(() => new SingleMbTilesSource(missingPath));

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("Could not load Mbtiles source file"));
            Assert.That(exception.InnerException, Is.Not.Null);
        });
    }

    private static TestMbTilesFixture CreateFixture()
    {
        return TestMbTilesFixture.Create(
            "metadata-and-point",
            new GlobalMercator.GeoExtent
            {
                West = -112.5,
                South = 39.5,
                East = -111.5,
                North = 40.5
            },
            latitude: 40,
            longitude: -112,
            zoom: 10);
    }
}
