using Mapbox.Vector.Tile;
using ProtoBuf;
using VectorTileRenderer.Benchmarks;

namespace VectorTileRenderer.Tests;

public class PbfWireTests
{
    [TestCase(new byte[] { 0x12, 0x00 })]
    [TestCase(new byte[] { 0x10, 0, 0x10, 1, 0x10, 2 })]
    [TestCase(new byte[] { 0x12, 2, 0, 1, 0x12, 1, 2 })]
    [TestCase(new byte[] { 0x22, 0, 0x22, 3, 9, 0, 0 })]
    [TestCase(new byte[] { 0x08, 1, 0x08, 2, 0x18, 3, 0x18, 1 })]
    [TestCase(new byte[] { 0x18, 123 })]
    [TestCase(new byte[] { 0x50, 7, 0x5a, 2, 1, 2, 0x63, 0x08, 1, 0x64 })]
    public void FeatureReader_MatchesDependencyForPackedUnpackedRepeatedAndUnknownFields(byte[] feature)
    {
        var bytes = WrapFeature(feature);
        using var expectedInput = new MemoryStream(bytes);
        var expected = Serializer.Deserialize<Tile>(expectedInput);
        using var actualInput = new MemoryStream(bytes);
        var actual = ProtobufReadExperiment.ReadTile(actualInput);
        var expectedFeature = expected.Layers[0].Features[0];
        var actualFeature = actual.Layers[0].Features[0];
        Assert.That(actualFeature.Id, Is.EqualTo(expectedFeature.Id));
        Assert.That(actualFeature.Type, Is.EqualTo(expectedFeature.Type));
        Assert.That(actualFeature.Tags, Is.EqualTo(expectedFeature.Tags));
        Assert.That(actualFeature.Geometry, Is.EqualTo(expectedFeature.Geometry));
        // Default-model serialization also verifies unknown-field preservation.
        Assert.That(Encode(actual), Is.EqualTo(Encode(expected)));
        Assert.That(actualInput.CanRead, Is.True);
    }

    [Test]
    public void FeatureReader_AppendsMixedPackedAndUnpackedSegmentsInWireOrder()
    {
        var bytes = WrapFeature([0x10, 0, 0x12, 1, 1, 0x10, 2, 0x12, 2, 3, 4]);
        using var input = new MemoryStream(bytes);
        Assert.That(ProtobufReadExperiment.ReadTile(input).Layers[0].Features[0].Tags,
            Is.EqualTo(new uint[] { 0, 1, 2, 3, 4 }));
    }

    [Test]
    public void Reader_MatchesDependencyForRandomizedModelsAndShortReads()
    {
        var random = new Random(38461);
        for (var trial = 0; trial < 100; trial++)
        {
            var tile = new Tile();
            for (var layerIndex = 0; layerIndex < random.Next(1, 5); layerIndex++)
            {
                var layer = new Tile.Layer { Name = "layer" + layerIndex, Version = 2, Extent = (uint)random.Next(1, 8193) };
                layer.Keys.AddRange(["name", "rank"]);
                layer.Values.Add(new Tile.Value { StringValue = "value" + trial });
                layer.Values.Add(new Tile.Value { IntValue = random.NextInt64() });
                for (var i = 0; i < random.Next(1, 50); i++)
                {
                    var feature = new Tile.Feature { Id = (ulong)random.NextInt64(), Type = Tile.GeomType.LineString };
                    feature.Tags.AddRange([0, 0, 1, 1]);
                    for (var j = 0; j < random.Next(0, 100); j++) feature.Geometry.Add((uint)random.Next());
                    layer.Features.Add(feature);
                }
                tile.Layers.Add(layer);
            }
            var bytes = Encode(tile);
            using var input = new ShortReadStream(bytes);
            var actual = ProtobufReadExperiment.ReadTile(input);
            Assert.That(Encode(actual), Is.EqualTo(bytes), "Trial " + trial);
            Assert.That(input.ReadCalls, Is.GreaterThan(1));
            Assert.That(input.CanRead, Is.True);
        }
    }

    [TestCase(new byte[] { })]
    [TestCase(new byte[] { 0x1a, 0 })]
    public void Reader_MatchesDependencyForEmptyAndDefaultModels(byte[] bytes)
    {
        using var expectedInput = new MemoryStream(bytes);
        using var actualInput = new MemoryStream(bytes);
        var expected = Serializer.Deserialize<Tile>(expectedInput);
        var actual = ProtobufReadExperiment.ReadTile(actualInput);
        Assert.That(Encode(actual), Is.EqualTo(Encode(expected)));
    }

    [TestCase(new byte[] { 0x12, 2, 0 })]
    [TestCase(new byte[] { 0x22, 1, 0x80 })]
    [TestCase(new byte[] { 0x10, 0x80 })]
    [TestCase(new byte[] { 0x18, 0x80 })]
    [TestCase(new byte[] { 0x00 })]
    public void FeatureReader_RejectsMalformedWireData(byte[] feature)
    {
        var bytes = WrapFeature(feature);
        using var expectedInput = new MemoryStream(bytes);
        Assert.Catch(() => Serializer.Deserialize<Tile>(expectedInput));
        using var actualInput = new MemoryStream(bytes);
        Assert.Catch(() => ProtobufReadExperiment.ReadTile(actualInput));
        Assert.That(actualInput.CanRead, Is.True);
    }

    [Test]
    public async Task FeatureReader_UsesIndependentModelsAcrossConcurrentRequests()
    {
        var bytes = WrapFeature([0x12, 2, 0, 1, 0x22, 3, 9, 0, 0]);
        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            using var input = new MemoryStream(bytes);
            return ProtobufReadExperiment.ReadTile(input).Layers[0].Features[0];
        })));
        results[0].Tags[0] = 99;
        Assert.That(results.Skip(1).All(feature => feature.Tags.SequenceEqual(new uint[] { 0, 1 })), Is.True);
        Assert.That(results.All(feature => feature.Geometry.SequenceEqual(new uint[] { 9, 0, 0 })), Is.True);
    }

    private static byte[] WrapFeature(byte[] feature)
    {
        // Small synthetic layer with explicit name/version and a single feature.
        var layer = new byte[] { 0x0a, 1, (byte)'x', 0x78, 2, 0x50, 7, 0x12, (byte)feature.Length }.Concat(feature).ToArray();
        return new byte[] { 0x50, 7, 0x1a, (byte)layer.Length }.Concat(layer).ToArray();
    }

    private static byte[] Encode(Tile tile)
    {
        using var output = new MemoryStream();
        Serializer.Serialize(output, tile);
        return output.ToArray();
    }

    private sealed class ShortReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public int ReadCalls { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCalls++;
            return base.Read(buffer, offset, Math.Min(count, 3));
        }

        public override int Read(Span<byte> buffer)
        {
            ReadCalls++;
            return base.Read(buffer[..Math.Min(buffer.Length, 3)]);
        }
    }
}
