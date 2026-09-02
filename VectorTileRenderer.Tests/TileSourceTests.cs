using NUnit.Framework;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Tests;

[TestFixture]
public class TileSourceTests
{
    [Test]
    public void TilePathResolver_ReplacesKnownTokensAndPreservesUnknownTokens()
    {
        var result = TilePathResolver.Resolve("root/{z}/{x}/{y}/{x}/{quadkey}.pbf", 12, 34, 5);

        Assert.That(result, Is.EqualTo("root/5/12/34/12/{quadkey}.pbf"));
    }

    [Test]
    public async Task PbfTileSource_ParsesGzippedAndUncompressedSampleTiles()
    {
        var gzipped = new PbfTileSource(TestAssets.GetPath("tiles", "zurich.pbf.gz"));
        var uncompressed = new PbfTileSource(TestAssets.GetPath("tiles", "newyork-mapbox.pbf"));

        var gzipTile = await gzipped.GetVectorTile(0, 0, 0);
        var plainTile = await uncompressed.GetVectorTile(0, 0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(gzipTile.Layers, Is.Not.Empty);
            Assert.That(plainTile.Layers, Is.Not.Empty);
            Assert.That(gzipTile.Layers.SelectMany(layer => layer.Features), Is.Not.Empty);
            Assert.That(plainTile.Layers.SelectMany(layer => layer.Features), Is.Not.Empty);
        });
    }

    [Test]
    public void PbfTileSource_Throws_WhenTileDataIsCorrupt()
    {
        var source = new PbfTileSource(new MemoryStream([0x01, 0x02, 0x03, 0x04]));

        Assert.That(async () => await source.GetVectorTile(0, 0, 0), Throws.Exception);
    }

    [Test]
    public async Task PbfTileSource_ReturnsNull_WhenNoPathOrStreamIsConfigured()
    {
        var source = new PbfTileSource(string.Empty);

        var tile = await source.GetVectorTile(0, 0, 0);

        Assert.That(tile, Is.Null);
    }

    [Test]
    public async Task RasterTileSource_ResolvesCoordinateTemplate()
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        var tilePath = Path.Combine(directory, "3", "1");
        Directory.CreateDirectory(tilePath);
        await File.WriteAllBytesAsync(Path.Combine(tilePath, "2.bin"), [1, 2, 3]);
        var source = new RasterTileSource(Path.Combine(directory, "{z}", "{x}", "{y}.bin"));

        using var stream = await source.GetTile(1, 2, 3);

        Assert.That(stream.Length, Is.EqualTo(3));
    }
}
