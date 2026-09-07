using SkiaSharp;

namespace VectorTileRenderer.Tests;

public class PathLabelTests
{
    [Test]
    public void DrawTextOnPath_RejectsSharpFinalTurn()
    {
        using var result = Render([new(30, 100), new(220, 100), new(220, 220)]);
        Assert.That(result.Pixels.All(p => p.Alpha == 0), Is.True);
    }

    [Test]
    public void DrawTextOnPath_RejectsZeroLengthRoad()
    {
        using var result = Render([new(100, 100), new(100, 100)]);
        Assert.That(result.Pixels.All(p => p.Alpha == 0), Is.True);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void DrawTextOnPath_RendersSameVisibleLabel_WhenRoadDirectionIsReversed(int shape)
    {
        List<Point> points = shape switch
        {
            0 => [new(30, 100), new(220, 100)],
            1 => [new(30, 100), new(120, 95), new(220, 100)],
            2 => [new(100, 220), new(100, 120), new(100, 30)],
            _ => [new(30, 120), new(30, 120), new(120, 90), new(220, 60)]
        };
        var original = points.ToArray();
        using var forward = Render(points);
        using var backward = Render(points.AsEnumerable().Reverse().ToList());
        Assert.That(points, Is.EqualTo(original), "Caller geometry must remain unchanged.");
        Assert.That(forward.Pixels.Count(p => p.Alpha > 0), Is.GreaterThan(100), "A blank image is not a pass.");
        Assert.That(backward.Pixels, Is.EqualTo(forward.Pixels));
        if (shape == 0)
        {
            Assert.That(Enumerable.Range(0, 100).Sum(y => Enumerable.Range(0, 256).Count(x => forward.GetPixel(x, y).Alpha > 0)),
                Is.GreaterThan(100), "Upright horizontal glyphs must extend above the baseline.");
        }
    }

    private static SKBitmap Render(List<Point> points)
    {
        var renderer = new TestCanvas();
        try
        {
            renderer.StartDrawing(256, 256);
            renderer.DrawTextOnPath(points, new Brush
            {
                Text = "WEST 31ST AVE",
                GlyphsDirectory = Path.GetDirectoryName(TestAssets.GetPath("styles", "fonts", "OpenSans Regular.ttf")),
                Paint = new Paint
                {
                    TextFont = ["OpenSans Regular"], TextSize = 16,
                    TextColor = Color.FromArgb(255, 0, 0, 0),
                    TextStrokeColor = Color.FromArgb(255, 255, 255, 255), TextStrokeWidth = 1
                }
            });
            return renderer.FinishDrawing().Copy();
        }
        finally { renderer.Release(); }
    }

    private sealed class TestCanvas : SkiaCanvas
    {
        public void Release()
        {
            surface?.Dispose();
            bitmap?.Dispose();
        }
    }
}
