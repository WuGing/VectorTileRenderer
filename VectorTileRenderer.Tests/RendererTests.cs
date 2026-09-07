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
        public void DrawImage(Stream imageStream, Brush style) { }
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
