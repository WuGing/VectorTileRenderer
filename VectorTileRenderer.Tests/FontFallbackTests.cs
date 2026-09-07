using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace VectorTileRenderer.Tests;

public class FontFallbackTests
{
    [Test]
    public void DrawText_UsesConfiguredFallbackWithActualGlyphCoverage()
    {
        using var primary = SKTypeface.FromFile(TestAssets.GetPath("styles", "fonts", "Metropolis Regular.otf"));
        using var primaryFont = new SKFont(primary);
        Assert.That(primaryFont.ContainsGlyphs("\u0416"), Is.False);
        using var fallback = Render("\u0416", ["Metropolis Regular", "OpenSans Regular"]);
        using var reference = Render("\u0416", ["OpenSans Regular"]);
        Assert.That(fallback.Pixels.Count(p => p.Alpha > 0), Is.GreaterThan(100));
        Assert.That(fallback.Pixels, Is.EqualTo(reference.Pixels));
    }

    [Test]
    public void Layout_PreservesMixedScriptsSurrogatesAndCombiningClusters()
    {
        const string text = "A\u0416 a\u0301 \U0001F680 tail";
        using var face = SKTypeface.FromFile(TestAssets.GetPath("styles", "fonts", "OpenSans Regular.ttf"));
        var elements = new List<string>();
        using var layout = new TextLayout(text, 16, element => { elements.Add(element); return face; });
        Assert.That(layout.Text, Is.EqualTo(text));
        Assert.That(elements, Does.Contain("\U0001F680"));
        Assert.That(elements, Does.Contain("a\u0301"));
        Assert.That(layout.Width, Is.GreaterThan(50));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Drawing_DoesNotTruncateCallerText(bool onRoad)
    {
        using var result = Render("A \U0001F680 tail", ["OpenSans Regular"], onRoad);
        Assert.That(result.Pixels.Count(p => p.Alpha > 0), Is.GreaterThan(100));
    }

    [Test]
    public void Layout_ShapesArabicLikeHarfBuzz()
    {
        const string text = "\u0633\u0644\u0627\u0645";
        using var face = SKFontManager.Default.MatchCharacter(0x0633);
        if (face == null) Assert.Ignore("No installed Arabic font for this platform validation.");
        using var font = new SKFont(face, 24) { Hinting = SKFontHinting.Normal };
        if (!font.ContainsGlyphs(text)) Assert.Ignore("Installed fallback does not cover the Arabic fixture.");
        using var layout = new TextLayout(text, 24, _ => face);
        using var shaper = new SKShaper(face);
        var expected = shaper.Shape(text, font);
        Assert.That(layout.Width, Is.EqualTo(expected.Width));
        Assert.That(expected.Codepoints.Select(g => (ushort)g), Is.Not.EqualTo(font.GetGlyphs(text)),
            "Shaped Arabic should differ from the unpositioned input glyph sequence.");
        using var placed = layout.Place(30, 60);
        using var actualBitmap = new SKBitmap(256, 128);
        using var actualCanvas = new SKCanvas(actualBitmap);
        actualCanvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        placed.Draw(actualCanvas, paint);
        var glyphs = expected.Codepoints.Select(g => (ushort)g).ToArray();
        var bytes = new byte[glyphs.Length * 2];
        Buffer.BlockCopy(glyphs, 0, bytes, 0, bytes.Length);
        using var blob = SKTextBlob.CreatePositioned(bytes, SKTextEncoding.GlyphId, font,
            expected.Points.Select(p => new SKPoint(p.X + 30, p.Y + 60)).ToArray());
        using var expectedBitmap = new SKBitmap(256, 128);
        using var expectedCanvas = new SKCanvas(expectedBitmap);
        expectedCanvas.Clear(SKColors.Transparent);
        expectedCanvas.DrawText(blob, 0, 0, paint);
        Assert.That(actualBitmap.Pixels, Is.EqualTo(expectedBitmap.Pixels));
    }

    private static SKBitmap Render(string text, string[] fonts, bool onRoad = false)
    {
        using var canvas = new SkiaCanvas();
        canvas.StartDrawing(512, 256);
        var brush = new Brush
        {
            Text = text,
            GlyphsDirectory = Path.GetDirectoryName(TestAssets.GetPath("styles", "fonts", "OpenSans Regular.ttf")),
            Paint = new Paint { TextFont = fonts, TextSize = 24, TextMaxWidth = 30, TextColor = Color.FromArgb(255, 0, 0, 0) }
        };
        if (onRoad) canvas.DrawTextOnPath([new(30, 128), new(480, 128)], brush);
        else canvas.DrawText(new Point(256, 128), brush);
        Assert.That(brush.Text, Is.EqualTo(text), "Fallback must not edit the input label.");
        return canvas.FinishDrawing();
    }
}
