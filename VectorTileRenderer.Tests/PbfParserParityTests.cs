using System.Reflection;
using Mapbox.Vector.Tile;
using ProtoBuf;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Tests;

public class PbfParserParityTests
{
    [Test]
    public async Task Decode_MatchesDependencyForAllValueTypesDefaultsAndPrecedence()
    {
        var values = new Tile.Value[]
        {
            new() { BoolValue = false }, new() { DoubleValue = -1.25 },
            new() { FloatValue = 0.0f }, new() { IntValue = -7 },
            new() { StringValue = "" }, new() { SintValue = long.MinValue },
            new() { UintValue = ulong.MaxValue }, new() { StringValue = "ignored", BoolValue = true }
        };
        var tile = CreateTile(values, Enumerable.Range(0, values.Length).SelectMany(i => new[] { (uint)i, (uint)i }).ToArray());
        var bytes = Encode(tile);
        using var input = new MemoryStream(bytes);
        var actual = await new PbfTileSource(input).GetVectorTile(0, 0, 0);
        using var expectedInput = new MemoryStream(bytes);
        var expected = VectorTileParser.Parse(expectedInput)[0].VectorTileFeatures[0].Attributes;
        var attributes = actual.Layers[0].Features[0].Attributes;
        Assert.That(attributes, Has.Count.EqualTo(expected.Count));
        foreach (var pair in expected)
        {
            Assert.That(attributes[pair.Key], Is.TypeOf(pair.Value.GetType()));
            Assert.That(attributes[pair.Key], Is.EqualTo(pair.Value));
        }
        Assert.That(input.CanRead, Is.True);
        Assert.That(actual.Layers[0].Name, Is.EqualTo("fixture"));
        Assert.That(actual.Layers[0].Features[0].Geometry, Is.Empty);
    }

    [TestCase(new uint[] { 0 })]
    [TestCase(new uint[] { 1, 0 })]
    [TestCase(new uint[] { 0, 1 })]
    public void Decode_RejectsInvalidTagPairsLikeDependency(uint[] tags)
    {
        var bytes = Encode(CreateTile([new Tile.Value { StringValue = "value" }], tags));
        using var expectedInput = new MemoryStream(bytes);
        var expected = Assert.Catch(() => VectorTileParser.Parse(expectedInput));
        using var actualInput = new MemoryStream(bytes);
        var actual = Assert.Catch(() => new PbfTileSource(actualInput).GetVectorTile(0, 0, 0).GetAwaiter().GetResult());
        Assert.That(actual, Is.TypeOf(expected.GetType()));
        Assert.That(actualInput.CanRead, Is.True);
    }

    [Test]
    public void Decode_RejectsReferencedEmptyValueLikeDependency()
    {
        var bytes = Encode(CreateTile([new Tile.Value()], [0, 0]));
        using var input = new MemoryStream(bytes);
        Assert.Throws<NotImplementedException>(() => new PbfTileSource(input).GetVectorTile(0, 0, 0).GetAwaiter().GetResult());
    }

    [Test]
    public void Decode_AllocatesLessThanDependencyForAttributeHeavyTile()
    {
        var tile = CreateTile([new Tile.Value { IntValue = 7 }], [0, 0, 0, 0, 0, 0, 0, 0]);
        for (var i = 0; i < 999; i++) tile.Layers[0].Features.Add(tile.Layers[0].Features[0]);
        var bytes = Encode(tile);
        var parse = typeof(PbfTileSource).Assembly.GetType("WuGing.VectorTileRenderer.Sources.PbfParser")
            .GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)
            .CreateDelegate<Func<Stream, List<Mapbox.Vector.Tile.VectorTileLayer>>>();
        for (var i = 0; i < 5; i++)
        {
            Measure(bytes, parse);
            Measure(bytes, VectorTileParser.Parse);
        }
        var baseline = Measure(bytes, VectorTileParser.Parse);
        var improved = Measure(bytes, parse);
        Assert.That(improved, Is.LessThan(baseline - 200_000), "Eliminate temporary tag enumerators/lists and attribute-list growth.");
    }

    [TestCase(0U)]
    [TestCase(4096U)]
    [TestCase(uint.MaxValue)]
    public async Task Decode_DirectConversionMatchesStagedCoordinatesAndOwnsItsContainers(uint extent)
    {
        var tile = CreateTile([new Tile.Value { StringValue = "first" }, new Tile.Value { StringValue = "last" }], [0, 0, 0, 1]);
        tile.Layers[0].Extent = extent;
        var feature = tile.Layers[0].Features[0];
        feature.Type = Tile.GeomType.Point;
        feature.Geometry.AddRange([17, 1, 1, 2, 2]);
        tile.Layers[0].Features.Add(feature);
        var bytes = Encode(tile);
        using var input = new MemoryStream(bytes);
        var actual = await new PbfTileSource(input).GetVectorTile(0, 0, 0);
        using var expectedInput = new MemoryStream(bytes);
        var expected = StagedDecode(expectedInput);
        Assert.That(actual.IsOverZoomed, Is.EqualTo(expected.IsOverZoomed));
        Assert.That(actual.Layers.Select(layer => layer.Name), Is.EqualTo(expected.Layers.Select(layer => layer.Name)));
        var actualFeature = actual.Layers[0].Features[0];
        var expectedFeature = expected.Layers[0].Features[0];
        Assert.That(actualFeature.Attributes, Is.EqualTo(expectedFeature.Attributes));
        Assert.That(actualFeature.Extent, Is.EqualTo(expectedFeature.Extent));
        Assert.That(actualFeature.GeometryType, Is.EqualTo(expectedFeature.GeometryType));
        Assert.That(CoordinateBits(actualFeature), Is.EqualTo(CoordinateBits(expectedFeature)));
        actualFeature.Attributes["key0"] = "edited";
        actualFeature.Geometry[0][0] = new Point(9, 9);
        Assert.That(actual.Layers[0].Features[1].Attributes["key0"], Is.EqualTo("last"));
        Assert.That(CoordinateBits(actual.Layers[0].Features[1]), Is.EqualTo(CoordinateBits(expectedFeature)));
        Assert.That(input.CanRead, Is.True);
    }

    [Test]
    public async Task Decode_UsesMissingNameFallbackForEmptyLayer()
    {
        using var input = new MemoryStream([0x1a, 2, 0x78, 2]);
        var tile = await new PbfTileSource(input).GetVectorTile(0, 0, 0);
        Assert.That(tile.Layers.Single().Name, Is.EqualTo("[Unnamed]"));
        Assert.That(tile.Layers.Single().Features, Is.Empty);
    }

    [Test]
    public void Decode_DirectConversionAvoidsIntermediateFeatureAllocations()
    {
        var tile = CreateTile([new Tile.Value { IntValue = 7 }], [0, 0, 0, 0, 0, 0, 0, 0]);
        for (var i = 0; i < 999; i++) tile.Layers[0].Features.Add(tile.Layers[0].Features[0]);
        var bytes = Encode(tile);
        var direct = typeof(PbfTileSource).Assembly.GetType("WuGing.VectorTileRenderer.Sources.PbfParser")
            .GetMethod("ParseRendererTile", BindingFlags.Public | BindingFlags.Static)
            .CreateDelegate<Func<Stream, VectorTile>>();
        for (var i = 0; i < 5; i++)
        {
            Measure(bytes, direct);
            Measure(bytes, StagedDecode);
        }
        var stagedBytes = Measure(bytes, StagedDecode);
        var directBytes = Measure(bytes, direct);
        Assert.That(directBytes, Is.LessThan(stagedBytes - 100_000));
    }

    private static readonly Func<List<Mapbox.Vector.Tile.VectorTileLayer>, VectorTile> convert =
        typeof(PbfTileSource).GetMethod("BaseTileToVector", BindingFlags.NonPublic | BindingFlags.Static)
            .CreateDelegate<Func<List<Mapbox.Vector.Tile.VectorTileLayer>, VectorTile>>();

    private static readonly Func<Stream, List<Mapbox.Vector.Tile.VectorTileLayer>> stagedParser =
        typeof(PbfTileSource).Assembly.GetType("WuGing.VectorTileRenderer.Sources.PbfParser")
            .GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)
            .CreateDelegate<Func<Stream, List<Mapbox.Vector.Tile.VectorTileLayer>>>();

    private static VectorTile StagedDecode(Stream input) => convert(stagedParser(input));

    private static long[] CoordinateBits(WuGing.VectorTileRenderer.VectorTileFeature feature) => feature.Geometry.SelectMany(part => part)
        .SelectMany(point => new[] { BitConverter.DoubleToInt64Bits(point.X), BitConverter.DoubleToInt64Bits(point.Y) }).ToArray();

    private static long Measure<T>(byte[] bytes, Func<Stream, T> parse)
    {
        using var input = new MemoryStream(bytes);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = parse(input);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(result);
        return allocated;
    }

    [Test]
    public void Decode_ReportsMalformedGeometryBeforeInvalidAttributeValues()
    {
        var tile = CreateTile([new Tile.Value()], [0, 0]);
        tile.Layers[0].Features[0].Geometry.Add(9);
        var bytes = Encode(tile);
        using var expectedInput = new MemoryStream(bytes);
        var expected = Assert.Catch(() => VectorTileParser.Parse(expectedInput));
        using var input = new MemoryStream(bytes);
        var actual = Assert.Catch(() => new PbfTileSource(input).GetVectorTile(0, 0, 0).GetAwaiter().GetResult());
        Assert.That(actual, Is.TypeOf(expected.GetType()));
    }

    private static Tile CreateTile(Tile.Value[] values, uint[] tags)
    {
        var tile = new Tile();
        var layer = new Tile.Layer { Name = "fixture", Version = 2 };
        for (var i = 0; i < values.Length; i++) layer.Keys.Add("key" + i);
        layer.Values.AddRange(values);
        var feature = new Tile.Feature();
        feature.Tags.AddRange(tags);
        layer.Features.Add(feature);
        tile.Layers.Add(layer);
        return tile;
    }

    private static byte[] Encode(Tile tile)
    {
        using var output = new MemoryStream();
        Serializer.Serialize(output, tile);
        return output.ToArray();
    }
}
