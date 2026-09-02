using NUnit.Framework;

namespace VectorTileRenderer.Tests;

[TestFixture]
public class GlobalMercatorTests
{
    private readonly GlobalMercator mercator = new();

    [Test]
    public void LatLonToMeters_ReturnsOrigin_ForZeroCoordinates()
    {
        var meters = mercator.LatLonToMeters(0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(meters.X, Is.EqualTo(0).Within(1e-9));
            Assert.That(meters.Y, Is.EqualTo(0).Within(1e-9));
        });
    }

    [TestCase(47.371143, 8.543924)]
    [TestCase(-33.8688, 151.2093)]
    [TestCase(0, -179.9)]
    public void MeterConversion_RoundTripsLatitudeAndLongitude(double latitude, double longitude)
    {
        var meters = mercator.LatLonToMeters(latitude, longitude);
        var result = mercator.MetersToLatLon(meters.X, meters.Y);

        Assert.Multiple(() =>
        {
            Assert.That(result.Y, Is.EqualTo(latitude).Within(1e-9));
            Assert.That(result.X, Is.EqualTo(longitude).Within(1e-9));
        });
    }

    [TestCase(0, 0, 1)]
    [TestCase(8580, 10646, 14)]
    [TestCase(345678, 456789, 20)]
    public void QuadTree_RoundTripsTileAddress(int x, int y, int zoom)
    {
        var quadTree = GlobalMercator.QuadTree(x, y, zoom);
        var tile = GlobalMercator.QuadTreeToTile(quadTree, zoom);

        Assert.Multiple(() =>
        {
            Assert.That(quadTree, Has.Length.EqualTo(zoom));
            Assert.That(tile.X, Is.EqualTo(x));
            Assert.That(tile.Y, Is.EqualTo(y));
        });
    }

    [Test]
    public void LatLonToTileXyz_FlipsTheTmsYCoordinate()
    {
        const int zoom = 14;
        var tms = mercator.LatLonToTile(47.371143, 8.543924, zoom);
        var xyz = mercator.LatLonToTileXYZ(47.371143, 8.543924, zoom);

        Assert.Multiple(() =>
        {
            Assert.That(xyz.X, Is.EqualTo(tms.X));
            Assert.That(xyz.Y, Is.EqualTo((1 << zoom) - tms.Y - 1));
        });
    }
}
