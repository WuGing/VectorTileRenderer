using System.Reflection;
using SkiaSharp;

namespace VectorTileRenderer.Tests;

public class PngEncodingTests
{
    private static readonly Func<SKBitmap, Style, SKData> encode = typeof(Renderer)
        .GetMethod("EncodeCachePng", BindingFlags.NonPublic | BindingFlags.Static)
        .CreateDelegate<Func<SKBitmap, Style, SKData>>();

    [TestCase(SKColorType.Bgra8888, SKAlphaType.Premul, false)]
    [TestCase(SKColorType.Rgba8888, SKAlphaType.Unpremul, false)]
    [TestCase(SKColorType.Rgba8888, SKAlphaType.Opaque, false)]
    [TestCase(SKColorType.Rgb565, SKAlphaType.Opaque, false)]
    [TestCase(SKColorType.Gray8, SKAlphaType.Opaque, false)]
    [TestCase(SKColorType.Alpha8, SKAlphaType.Premul, false)]
    [TestCase(SKColorType.RgbaF16, SKAlphaType.Premul, true)]
    [TestCase(SKColorType.Bgra8888, SKAlphaType.Premul, true)]
    public void EncodeCachePng_PreservesLegacyDecodedPixelsAndBitmapOwnership(SKColorType colorType, SKAlphaType alphaType, bool linear)
    {
        using var colorSpace = linear ? SKColorSpace.CreateSrgbLinear() : SKColorSpace.CreateSrgb();
        using var bitmap = new SKBitmap(new SKImageInfo(33, 17, colorType, alphaType, colorSpace));
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
            bitmap.SetPixel(x, y, new SKColor((byte)(x * 7), (byte)(y * 13), (byte)(x * 3 + y), (byte)(x % 4 * 85)));
        var originalPixels = bitmap.Pixels;
        using var image = SKImage.FromBitmap(bitmap);
        using var baselineData = image.Encode(SKEncodedImageFormat.Png, 100);
        Assert.That(baselineData, Is.Not.Null);
        using var expected = SKBitmap.Decode(baselineData);
        using var actualData = encode(bitmap, CreateStyle(false));
        using var actual = SKBitmap.Decode(actualData);
        Assert.That(actual, Is.Not.Null);
        Assert.That(actual.Width, Is.EqualTo(expected.Width));
        Assert.That(actual.Height, Is.EqualTo(expected.Height));
        Assert.That(actual.Pixels, Is.EqualTo(expected.Pixels));
        Assert.That(bitmap.Handle, Is.Not.EqualTo(IntPtr.Zero));
        Assert.That(bitmap.Pixels, Is.EqualTo(originalPixels));
    }

    [Test]
    public void EncodeCachePng_UsesCompactFilteringForRasterContainingStyles()
    {
        using var bitmap = new SKBitmap(128, 128);
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
            bitmap.SetPixel(x, y, new SKColor((byte)(x * 2), (byte)(y * 2), (byte)(x + y)));
        using var image = SKImage.FromBitmap(bitmap);
        using var baseline = image.Encode(SKEncodedImageFormat.Png, 100);
        using var actual = encode(bitmap, CreateStyle(true));
        Assert.That(actual.ToArray(), Is.EqualTo(baseline.ToArray()));
    }

    private static Style CreateStyle(bool raster)
    {
        var style = new Style(TestAssets.WriteTemporaryStyle("""{"sources":{},"layers":[]}"""));
        style.Layers.Add(new Layer { Type = "background" });
        style.Layers.Add(new Layer { Type = "fill", Source = new Source { Type = "vector" } });
        if (raster) style.Layers.Add(new Layer { Type = "raster", Source = new Source { Type = "raster" } });
        return style;
    }
}
