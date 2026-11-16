using System.Reflection;
using Mapbox.Vector.Tile;

namespace VectorTileRenderer.Tests;

public class PbfGeometryDecoderTests
{
    private static readonly Action<IReadOnlyList<uint>, Tile.GeomType, uint, List<List<Point>>> decode = typeof(Style).Assembly
        .GetType("WuGing.VectorTileRenderer.Sources.PbfGeometryDecoder")
        .GetMethod("Decode", BindingFlags.Static | BindingFlags.Public)
        .CreateDelegate<Action<IReadOnlyList<uint>, Tile.GeomType, uint, List<List<Point>>>>();

    [TestCase(Tile.GeomType.Unknown)]
    [TestCase(Tile.GeomType.Point)]
    [TestCase(Tile.GeomType.LineString)]
    [TestCase(Tile.GeomType.Polygon)]
    public void Decode_MatchesDependencyForSegmentsClosuresAndMalformedCommands(Tile.GeomType type)
    {
        uint[][] inputs =
        [
            [], [9, 0, 0], [17, 1, 2, 2, 1],
            [9, 0, 0, 10, 8192, 0, 9, 0, 8192, 10, 8191, 0],
            [9, 0, 0, 26, 8192, 0, 0, 8192, 8191, 0, 15],
            [9], [9, 0], [15], [15, 0], [9, 0, 0, 31, 0],
            [0, 0, 0], [11, uint.MaxValue, uint.MaxValue],
            [9, uint.MaxValue, uint.MaxValue, 10, uint.MaxValue, uint.MaxValue]
        ];
        foreach (var extent in new[] { 0U, 1U, 4096U, uint.MaxValue })
        foreach (var input in inputs) AssertParity(input, type, extent);
    }

    [Test]
    public void Decode_MatchesDependencyForSeededMalformedWordSequences()
    {
        var random = new Random(718319);
        for (var i = 0; i < 2000; i++)
        {
            // A zero-count ClosePath can spin in the dependency. Keep differential inputs bounded.
            var input = Enumerable.Range(0, random.Next(61)).Select(_ => (uint)random.Next(65))
                .Select(word => word == 7 ? 15U : word).ToArray();
            AssertParity(input, (Tile.GeomType)random.Next(6), (uint)random.Next(8193));
        }
    }

    [Test]
    public void Decode_PreservesCoordinateBitsAcrossLargeDeltasAndMultipartFeatures()
    {
        var words = new List<uint>();
        for (var part = 0; part < 20; part++)
        {
            words.AddRange([9, uint.MaxValue, uint.MaxValue - 1, (100U << 3) | 2]);
            for (var i = 0; i < 100; i++) words.AddRange([uint.MaxValue, uint.MaxValue - 1]);
            words.Add(15);
        }
        AssertParity(words.ToArray(), Tile.GeomType.Polygon, 4093);
    }

    [TestCase(Tile.GeomType.Point)]
    [TestCase(Tile.GeomType.Polygon)]
    public void Decode_RejectsNonadvancingZeroCountCloseCommands(Tile.GeomType type)
    {
        Assert.Throws<InvalidDataException>(() => decode(new List<uint> { 7, 0 }, type, 4096, new()));
    }

    private static void AssertParity(uint[] words, Tile.GeomType type, uint extent)
    {
        // List indexing matches the dependency's protobuf model, including exception types.
        var input = words.ToList();
        List<List<Point>> expected = null;
        Exception expectedError = null;
        try
        {
            expected = GeometryParser.ParseGeometry(input, type)
                .Select(segment => segment.Select(point => new Point(point.X / (double)extent, point.Y / (double)extent)).ToList()).ToList();
        }
        catch (Exception error)
        {
            expectedError = error;
        }
        var actual = new List<List<Point>>();
        if (expectedError != null)
        {
            Assert.That(Assert.Catch(() => decode(input, type, extent, actual)), Is.TypeOf(expectedError.GetType()));
            return;
        }
        decode(input, type, extent, actual);
        Assert.That(actual.Select(part => part.Count), Is.EqualTo(expected.Select(part => part.Count)));
        Assert.That(Bits(actual), Is.EqualTo(Bits(expected)));
    }

    private static long[] Bits(List<List<Point>> geometry) => geometry.SelectMany(part => part)
        .SelectMany(point => new[] { BitConverter.DoubleToInt64Bits(point.X), BitConverter.DoubleToInt64Bits(point.Y) }).ToArray();
}
