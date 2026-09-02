using NUnit.Framework;

namespace VectorTileRenderer.Tests;

[TestFixture]
public class VectorTileTests
{
    [Test]
    public void ApplyExtent_MapsSelectedRegionToFullFeatureExtent()
    {
        var feature = new VectorTileFeature
        {
            Extent = 4096,
            GeometryType = "LineString",
            Attributes = new Dictionary<string, object> { ["name"] = "road" },
            Geometry =
            [
                [new Point(0.25, 0.5), new Point(0.75, 1.0)]
            ]
        };
        var tile = new VectorTile
        {
            Layers =
            [
                new VectorTileLayer
                {
                    Name = "transportation",
                    Features = [feature]
                }
            ]
        };

        var result = tile.ApplyExtent(new Rect(0.25, 0.5, 0.5, 0.5));
        var mapped = result.Layers[0].Features[0];

        Assert.Multiple(() =>
        {
            Assert.That(mapped.Geometry[0][0], Is.EqualTo(new Point(0, 0)));
            Assert.That(mapped.Geometry[0][1], Is.EqualTo(new Point(4096, 4096)));
            Assert.That(mapped.Attributes, Is.EqualTo(feature.Attributes));
            Assert.That(mapped.Attributes, Is.Not.SameAs(feature.Attributes));
            Assert.That(mapped.Geometry, Is.Not.SameAs(feature.Geometry));
        });
    }

    [Test]
    public void ApplyExtent_MapsCollapsedAxisToZero()
    {
        var tile = new VectorTile
        {
            Layers =
            [
                new VectorTileLayer
                {
                    Features =
                    [
                        new VectorTileFeature
                        {
                            Extent = 256,
                            Geometry = [[new Point(5, 4)]]
                        }
                    ]
                }
            ]
        };

        var result = tile.ApplyExtent(new Rect(5, 2, 0, 4));

        Assert.That(result.Layers[0].Features[0].Geometry[0][0], Is.EqualTo(new Point(0, 128)));
    }
}
