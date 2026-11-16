using System.Text;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Tests;

public class PbfConversionTests
{
    [Test]
    public void DefaultFeature_CreatesIndependentMutableContainers()
    {
        var first = new VectorTileFeature();
        var second = new VectorTileFeature();
        first.Attributes["name"] = "one";
        first.Geometry.Add([new Point(0.5, 0.25)]);
        Assert.That(second.Attributes, Is.Empty);
        Assert.That(second.Geometry, Is.Empty);
        Assert.That(typeof(VectorTileFeature).GetConstructor(Type.EmptyTypes), Is.Not.Null);
    }

    [Test]
    public async Task Decode_PreservesTypedAttributesDuplicateKeysAndMultipartGeometry()
    {
        using var data = new MemoryStream(CreateTile());
        var tile = await new PbfTileSource(data).GetVectorTile(0, 0, 0);
        Assert.That(data.CanRead, Is.True, "The supplied stream remains caller-owned.");
        Assert.That(tile.Layers.Select(layer => layer.Name), Is.EqualTo(new[] { "fixture", "empty" }));
        Assert.That(tile.Layers[1].Features, Is.Empty);
        var features = tile.Layers[0].Features;
        Assert.That(features.Select(feature => feature.GeometryType), Is.EqualTo(new[] { "Point", "LineString", "Polygon" }));
        Assert.That(features.All(feature => feature.Extent == 1), Is.True);
        Assert.That(features[0].Attributes, Has.Count.EqualTo(4));
        Assert.That(features[0].Attributes["name"], Is.EqualTo("last"));
        Assert.That(features[0].Attributes["rank"], Is.TypeOf<long>().And.EqualTo(7L));
        Assert.That(features[0].Attributes["score"], Is.TypeOf<double>().And.EqualTo(1.25));
        Assert.That(features[0].Attributes["enabled"], Is.TypeOf<bool>().And.EqualTo(true));
        Assert.That(features[0].Geometry.SelectMany(part => part), Is.EqualTo(new[] { new Point(0.0625, 0.125), new Point(0.03125, 0.140625) }));
        Assert.That(features[1].Geometry, Has.Count.EqualTo(2));
        Assert.That(features[1].Geometry[0], Is.EqualTo(new[] { new Point(0, 0), new Point(1, 0) }));
        Assert.That(features[1].Geometry[1], Is.EqualTo(new[] { new Point(1, 1), new Point(0, 1) }));
        Assert.That(features[2].Geometry[0][0], Is.EqualTo(features[2].Geometry[0][^1]));
        features[0].Attributes["name"] = "edited";
        features[0].Geometry[0][0] = new Point(9, 9);
        Assert.That(features[1].Attributes["name"], Is.EqualTo("last"));
        Assert.That(features[1].Geometry[0][0], Is.EqualTo(new Point(0, 0)));
    }

    private static byte[] CreateTile()
    {
        using var layer = new MemoryStream();
        Blob(layer, 1, Encoding.UTF8.GetBytes("fixture"));
        var geometries = new uint[][]
        {
            [17, 512, 1024, 255, 128],
            [9, 0, 0, 10, 8192, 0, 9, 0, 8192, 10, 8191, 0],
            [9, 0, 0, 26, 8192, 0, 0, 8192, 8191, 0, 15]
        };
        for (var i = 0; i < geometries.Length; i++)
        {
            using var feature = new MemoryStream();
            Number(feature, 1, (uint)i + 1);
            Blob(feature, 2, Packed([0, 0, 1, 1, 2, 2, 3, 3, 0, 4]));
            Number(feature, 3, (uint)i + 1);
            Blob(feature, 4, Packed(geometries[i]));
            Blob(layer, 2, feature.ToArray());
        }
        foreach (var key in new[] { "name", "rank", "score", "enabled" }) Blob(layer, 3, Encoding.UTF8.GetBytes(key));
        foreach (var value in new object[] { "first", 7L, 1.25, true, "last" })
        {
            using var encoded = new MemoryStream();
            if (value is string text) Blob(encoded, 1, Encoding.UTF8.GetBytes(text));
            else if (value is long number) Number(encoded, 4, (uint)number);
            else if (value is bool boolean) Number(encoded, 7, boolean ? 1U : 0U);
            else
            {
                Varint(encoded, (3U << 3) | 1);
                using var writer = new BinaryWriter(encoded, Encoding.UTF8, true);
                writer.Write((double)value);
            }
            Blob(layer, 4, encoded.ToArray());
        }
        Number(layer, 5, 4096);
        Number(layer, 15, 2);
        using var empty = new MemoryStream();
        Blob(empty, 1, Encoding.UTF8.GetBytes("empty"));
        Number(empty, 5, 4096);
        Number(empty, 15, 2);
        using var tile = new MemoryStream();
        Blob(tile, 3, layer.ToArray());
        Blob(tile, 3, empty.ToArray());
        return tile.ToArray();
    }

    private static byte[] Packed(uint[] values)
    {
        using var stream = new MemoryStream();
        foreach (var value in values) Varint(stream, value);
        return stream.ToArray();
    }

    private static void Number(Stream stream, uint field, uint value)
    {
        Varint(stream, field << 3);
        Varint(stream, value);
    }

    private static void Blob(Stream stream, uint field, byte[] value)
    {
        Varint(stream, (field << 3) | 2);
        Varint(stream, (uint)value.Length);
        stream.Write(value);
    }

    private static void Varint(Stream stream, uint value)
    {
        while (value >= 128)
        {
            stream.WriteByte((byte)((value & 127) | 128));
            value >>= 7;
        }
        stream.WriteByte((byte)value);
    }
}
