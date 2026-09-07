using SkiaSharp;

namespace VectorTileRenderer.Tests;

public class PointLabelTests
{
    [TestCase(3, 128, 0, false)]
    [TestCase(253, 128, 0, false)]
    [TestCase(128, 0, 0, false)]
    [TestCase(128, 253, 0, false)]
    [TestCase(210, 128, 3, true)]
    public void DrawText_RejectsWholeLabel_WhenInkWouldCrossTileEdge(int x, int y, int offset, bool overzoom)
    {
        using var result = Render(x, y, offset, overzoom);
        Assert.That(result.Pixels.All(p => p.Alpha == 0), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DrawText_KeepsInteriorMultilineLabel(bool overzoom)
    {
        using var result = Render(128, 128, 0, overzoom);
        Assert.That(result.Pixels.Count(p => p.Alpha > 0), Is.GreaterThan(100));
    }

    [TestCase(TextAlignment.Left, 230)]
    [TestCase(TextAlignment.Right, 20)]
    public void DrawText_UsesJustificationWhenCheckingTileEdge(TextAlignment alignment, int x)
    {
        using var result = Render(x, 128, 0, false, alignment);
        Assert.That(result.Pixels.All(p => p.Alpha == 0), Is.True);
    }

    private static SKBitmap Render(int x, int y, int offset, bool overzoom, TextAlignment alignment = TextAlignment.Center)
    {
        var renderer = new TestCanvas { ClipOverflow = overzoom };
        try
        {
            renderer.StartDrawing(256, 256);
            renderer.DrawText(new Point(x, y), new Brush
            {
                Text = "University\nMuseum",
                GlyphsDirectory = Path.GetDirectoryName(TestAssets.GetPath("styles", "fonts", "OpenSans Regular.ttf")),
                Paint = new Paint
                {
                    TextFont = ["OpenSans Regular"], TextSize = 16, TextJustify = alignment,
                    TextOffset = new Point(offset, 0), TextMaxWidth = 20,
                    TextColor = Color.FromArgb(255, 0, 0, 0),
                    TextStrokeColor = Color.FromArgb(255, 255, 255, 255), TextStrokeWidth = 3
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
