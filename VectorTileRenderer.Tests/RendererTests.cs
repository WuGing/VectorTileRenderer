using NUnit.Framework;
using SkiaSharp;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Tests;

[TestFixture]
[NonParallelizable]
public class RendererTests
{
    [TearDown]
    public void TearDown()
    {
        Renderer.ProfileSink = null;
        Renderer.CurrentBackendHint = null;
    }

    [Test]
    public async Task Render_DrawsMatchingGeometryAndReportsProfile()
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var canvas = new RecordingCanvas();
        Renderer.RenderProfile profile = null;
        Renderer.CurrentBackendHint = "Test";
        Renderer.ProfileSink = value => profile = value;

        using var bitmap = await Renderer.Render(style, canvas, 1, 2, 8, 256, 256);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.LineStrings, Has.Count.EqualTo(1));
            Assert.That(canvas.LineStrings[0], Is.EqualTo(new[] { new Point(0, 0), new Point(256, 256) }));
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.Backend, Is.EqualTo("Test"));
            Assert.That(profile.GeometryDrawCallCount, Is.EqualTo(1));
            Assert.That(profile.FeatureAcceptedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Render_ReturnsNull_WhenVectorTileIsMissing()
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(null));
        var canvas = new RecordingCanvas();

        var bitmap = await Renderer.Render(style, canvas, 1, 2, 8, 256, 256);

        Assert.Multiple(() =>
        {
            Assert.That(bitmap, Is.Null);
            Assert.That(canvas.FinishCalled, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Render_PropagatesFinishFailure_WithoutPublishingSuccess(bool cached)
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var failure = new InvalidOperationException("Simulated pixel readback failure");
        var canvas = new RecordingCanvas { FinishFailure = failure };
        Renderer.RenderProfile profile = null;
        Renderer.ProfileSink = value => profile = value;
        var cachePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"failed-render-{Guid.NewGuid():N}");

        try
        {
            await Assert.ThatAsync(async () =>
            {
                using var bitmap = cached
                    ? await Renderer.RenderCached(cachePath, style, canvas, 1, 2, 8, 256, 256)
                    : await Renderer.Render(style, canvas, 1, 2, 8, 256, 256);
            }, Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo(failure.Message));
            Assert.That(canvas.FinishCalled, Is.True);
            Assert.That(profile, Is.Null);
            if (cached)
            {
                Assert.That(Directory.GetFiles(cachePath), Is.Empty);
            }
        }
        finally
        {
            // Remove only this test's empty directory; never recursively remove output.
            if (Directory.Exists(cachePath) && !Directory.EnumerateFileSystemEntries(cachePath).Any())
            {
                Directory.Delete(cachePath);
            }
        }
    }

    [TestCase(0)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public async Task RenderCached_IgnoresTilesFromBeforeLabelPlacementFix(int renderVersion)
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var cachePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"legacy-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cachePath);
        object legacyBundle = renderVersion > 0
            ? new { RenderVersion = renderVersion, style.Hash, sizeX = 256d, sizeY = 256d, scale = 1d, layerString = "" }
            : new { style.Hash, sizeX = 256d, sizeY = 256d, scale = 1d, layerString = "" };
        var legacyKey = System.Text.Json.JsonSerializer.Serialize(legacyBundle);
        var legacyPath = Path.Combine(cachePath, "1x2-8-" + Utils.Sha256(legacyKey).Substring(0, 12) + ".png");
        using (var oldBitmap = new SKBitmap(1, 1))
        using (var data = oldBitmap.Encode(SKEncodedImageFormat.Png, 100))
        {
            File.WriteAllBytes(legacyPath, data.ToArray());
        }
        // A fresh render throws; loading the legacy PNG would incorrectly succeed.
        var canvas = new RecordingCanvas { FinishFailure = new InvalidOperationException("Fresh render") };
        try
        {
            await Assert.ThatAsync(() => Renderer.RenderCached(cachePath, style, canvas, 1, 2, 8, 256, 256),
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Fresh render"));
            Assert.That(canvas.FinishCalled, Is.True);
        }
        finally
        {
            File.Delete(legacyPath);
            Directory.Delete(cachePath);
        }
    }

    [Test]
    public async Task RenderCached_CompletesPublicationBeforeReturningCallerOwnedBitmap()
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "cache-ownership-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var canvas = new SkiaCanvas())
            using (var image = await Renderer.RenderCached(directory, style, canvas, 1, 2, 8, 256, 256))
            {
                Assert.That(Directory.GetFiles(directory, "*.png"), Has.Length.EqualTo(1));
            }
            var shouldNotRender = new RecordingCanvas { FinishFailure = new InvalidOperationException("Unexpected cache miss") };
            using var cached = await Renderer.RenderCached(directory, style, shouldNotRender, 1, 2, 8, 256, 256);
            Assert.That(cached.Width, Is.EqualTo(256));
            Assert.That(shouldNotRender.FinishCalled, Is.False);
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Render_KeepsSharedRasterAliveUntilRequestEnds(bool failFinish)
    {
        var path = TestAssets.WriteTemporaryStyle("""
            { "sources": { "tiles": { "type": "raster" } }, "layers": [
              { "id": "one", "type": "raster", "source": "tiles" },
              { "id": "two", "type": "raster", "source": "tiles" }
            ] }
            """);
        var style = new Style(path);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        style.SetSourceProvider("tiles", new RasterSource(stream));
        var canvas = new RecordingCanvas { FinishFailure = failFinish ? new InvalidOperationException("Finish failed") : null };
        if (failFinish)
        {
            await Assert.ThatAsync(() => Renderer.Render(style, canvas, 1, 2, 8, 256, 256),
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Finish failed"));
        }
        else
        {
            using var image = await Renderer.Render(style, canvas, 1, 2, 8, 256, 256);
        }
        Assert.That(canvas.ReadableRasters, Is.EqualTo(new[] { true, true }));
        Assert.That(stream.CanRead, Is.False, "Renderer must release provider streams on success and failure.");
    }

    private sealed class RasterSource(Stream stream) : ITileSource
    {
        public Task<Stream> GetTile(int x, int y, int z) => Task.FromResult(stream);
    }

    [Test]
    public async Task RenderCached_ConcurrentMissesPublishOneCompleteFile()
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "cache-concurrent-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
            {
                using var canvas = new SkiaCanvas();
                using var image = await Renderer.RenderCached(directory, style, canvas, 1, 2, 8, 256, 256);
                Assert.That(image.Width, Is.EqualTo(256));
            })));
            var files = Directory.GetFiles(directory);
            Assert.That(files, Has.Length.EqualTo(1));
            using var decoded = SKBitmap.Decode(files[0]);
            Assert.That(decoded.Width, Is.EqualTo(256));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
    }

    private static Style CreateLineStyle()
    {
        var path = TestAssets.WriteTemporaryStyle("""
            {
              "sources": { "tiles": { "type": "vector" } },
              "layers": [
                {
                  "id": "roads",
                  "type": "line",
                  "source": "tiles",
                  "source-layer": "roads",
                  "paint": { "line-color": "#000000", "line-width": 1 }
                }
              ]
            }
            """);
        return new Style(path);
    }

    private static VectorTile CreateLineTile()
    {
        return new VectorTile
        {
            Layers =
            [
                new VectorTileLayer
                {
                    Name = "roads",
                    Features =
                    [
                        new VectorTileFeature
                        {
                            Extent = 1,
                            GeometryType = "LineString",
                            Geometry = [[new Point(0, 0), new Point(1, 1)]]
                        }
                    ]
                }
            ]
        };
    }

    private sealed class StubVectorTileSource(VectorTile tile) : IVectorTileSource
    {
        public Task<Stream> GetTile(int x, int y, int z) => Task.FromResult<Stream>(null);

        public Task<VectorTile> GetVectorTile(int x, int y, int z) => Task.FromResult(tile);
    }

    private sealed class RecordingCanvas : ICanvas
    {
        public bool ClipOverflow { get; set; }
        public List<List<Point>> LineStrings { get; } = [];
        public bool FinishCalled { get; private set; }
        public Exception FinishFailure { get; set; }

        public void StartDrawing(double sizeX, double sizeY) { }
        public void DrawBackground(Brush style) { }
        public void DrawLineString(List<Point> geometry, Brush style) => LineStrings.Add(geometry);
        public void DrawPolygon(List<Point> geometry, Brush style) { }
        public void DrawPoint(Point geometry, Brush style) { }
        public void DrawText(Point geometry, Brush style) { }
        public void DrawTextOnPath(List<Point> geometry, Brush style) { }
        public List<bool> ReadableRasters { get; } = new();
        public void DrawImage(Stream imageStream, Brush style) => ReadableRasters.Add(imageStream.CanRead);
        public void DrawUnknown(List<List<Point>> geometry, Brush style) { }

        public SKBitmap FinishDrawing()
        {
            FinishCalled = true;
            if (FinishFailure != null)
            {
                throw FinishFailure;
            }
            return new SKBitmap(1, 1);
        }
    }
}
