using SkiaSharp;

namespace VectorTileRenderer.Tests;

public class CanvasOwnershipTests
{
    [Test]
    public void FinishDrawing_TransfersBitmapAcrossReuseAndDisposal()
    {
        var canvas = new SkiaCanvas();
        canvas.StartDrawing(16, 16);
        canvas.DrawBackground(new Brush { Paint = new Paint { BackgroundColor = Color.FromRgb(255, 0, 0) } });
        using var first = canvas.FinishDrawing();
        canvas.StartDrawing(8, 8);
        using var second = canvas.FinishDrawing();
        canvas.Dispose();
        canvas.Dispose();
        Assert.That(first.GetPixel(0, 0), Is.EqualTo(SKColors.Red));
        Assert.That(second.Width, Is.EqualTo(8));
        Assert.That(() => canvas.StartDrawing(8, 8), Throws.TypeOf<ObjectDisposedException>());
    }

    [Test]
    public void RepeatedRenders_ReturnIndependentOwnedBitmaps()
    {
        using var canvas = new SkiaCanvas();
        for (var i = 0; i < 500; i++)
        {
            canvas.StartDrawing(32, 32);
            using var image = canvas.FinishDrawing();
            Assert.That(image.Width, Is.EqualTo(32));
        }
    }
}
