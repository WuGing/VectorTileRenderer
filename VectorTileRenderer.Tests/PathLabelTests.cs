using SkiaSharp;

namespace VectorTileRenderer.Tests;

public class PathLabelTests
{
    [Test]
    public void DrawTextOnPath_DoesNotDrawPartialLabel_WhenRoadIsShorterThanMeasuredText()
    {
        using var result = Render([new(30, 100), new(90, 100)]);
        Assert.That(result.Pixels.All(p => p.Alpha == 0), Is.True, "Do not draw just the first few letters.");
    }

    [Test]
    public void DrawTextOnPath_UsesRigidGlyphsCenteredOnTheRoad()
    {
        List<Point> road = [new(20, 120), new(100, 110), new(180, 110), new(240, 115)];
        using var actual = Render(road);
        using var expected = new SKBitmap(256, 256);
        using var canvas = new SKCanvas(expected);
        canvas.Clear(SKColors.Transparent);
        using var typeface = SKTypeface.FromFile(TestAssets.GetPath("styles", "fonts", "OpenSans Regular.ttf"));
        using var font = new SKFont(typeface, 16) { Hinting = SKFontHinting.Normal };
        using var path = new SKPath();
        path.MoveTo(20, 120); path.LineTo(100, 110); path.LineTo(180, 110); path.LineTo(240, 115);
        using var label = SKTextBlob.CreatePathPositioned("WEST 31ST AVE", font, path, SKTextAlign.Center);
        using var halo = new SKPaint { IsAntialias = true, IsStroke = true, StrokeWidth = 1, Color = SKColors.White };
        using var paint = new SKPaint { IsAntialias = true, Color = SKColors.Black };
        canvas.DrawText(label, 0, 0, halo); canvas.DrawText(label, 0, 0, paint);
        Assert.That(actual.Pixels.Count(p => p.Alpha > 0), Is.GreaterThan(100));
        Assert.That(actual.Pixels, Is.EqualTo(expected.Pixels));
    }

    [Test]
    public void DrawTextOnPath_DoesNotJoinDisconnectedVisibleRoadSegments()
    {
        using var result = Render([new(20, 30), new(-30, 30), new(-30, 220), new(20, 220)]);
        Assert.That(result.Pixels.All(p => p.Alpha == 0), Is.True);
    }

    [Test]
    public void DrawTextOnPath_DoesNotCutGlyphsAtTileEdge()
    {
        using var result = Render([new(20, 3), new(230, 3)]);
        Assert.That(result.Pixels.All(p => p.Alpha == 0), Is.True);
    }

    [Test]
    public void DrawTextOnPath_UsesStraightStretchBeforeSharpFinalTurn()
    {
        using var result = Render([new(30, 100), new(220, 100), new(220, 220)]);
        using var expected = Render([new(30, 100), new(220, 100)]);
        Assert.That(result.Pixels, Is.EqualTo(expected.Pixels));
        using var reversed = Render([new(220, 220), new(220, 100), new(30, 100)]);
        Assert.That(reversed.Pixels, Is.EqualTo(expected.Pixels));
    }

    [Test]
    public void DrawTextOnPath_RejectsAbruptBendInsideLabel()
    {
        using var result = Render([new(30, 100), new(110, 100), new(180, 140)]);
        Assert.That(result.Pixels.All(p => p.Alpha == 0), Is.True);
    }

    [Test]
    public void DrawTextOnPath_RejectsAccumulatedBending()
    {
        var road = Enumerable.Range(0, 16).Select(i =>
            new Point(128 + 95 * Math.Cos(i * Math.PI / 30), 128 + 95 * Math.Sin(i * Math.PI / 30))).ToList();
        using var result = Render(road);
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
            return renderer.FinishDrawing();
        }
        finally { renderer.Release(); }
    }

    private sealed class TestCanvas : SkiaCanvas
    {
        public void Release()
        {
            Dispose();
        }
    }
}
