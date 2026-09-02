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
            return new SKBitmap(1, 1);
        }
    }
}
