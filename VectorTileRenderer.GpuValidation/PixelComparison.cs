using SkiaSharp;

namespace WuGing.VectorTileRenderer.GpuValidation;

internal sealed record PixelComparison(double MeanChannelError, double PixelsOver32Percent, bool Passed)
{
    // Declared before measurement: permit AA edge variation, not a missing feature/blank image.
    public static PixelComparison Compare(SKBitmap cpu, SKBitmap gpu, string differencePath)
    {
        if (cpu.Width != gpu.Width || cpu.Height != gpu.Height) throw new InvalidOperationException("Size mismatch.");
        using var difference = new SKBitmap(cpu.Width, cpu.Height);
        long error = 0;
        var largeErrors = 0;
        var foreground = 0;
        var background = cpu.GetPixel(0, 0);
        for (var y = 0; y < cpu.Height; y++)
        {
            for (var x = 0; x < cpu.Width; x++)
            {
                var a = cpu.GetPixel(x, y);
                var b = gpu.GetPixel(x, y);
                var r = Math.Abs(a.Red - b.Red);
                var g = Math.Abs(a.Green - b.Green);
                var blue = Math.Abs(a.Blue - b.Blue);
                var alpha = Math.Abs(a.Alpha - b.Alpha);
                error += r + g + blue + alpha;
                if (Math.Max(Math.Max(r, g), Math.Max(blue, alpha)) > 32) largeErrors++;
                if (a != background) foreground++;
                difference.SetPixel(x, y, new SKColor((byte)Math.Min(255, r * 4),
                    (byte)Math.Min(255, g * 4), (byte)Math.Min(255, blue * 4)));
            }
        }
        Save(difference, differencePath);
        var pixels = cpu.Width * cpu.Height;
        var mean = error / (pixels * 4.0);
        var percent = largeErrors * 100.0 / pixels;
        return new PixelComparison(mean, percent, foreground > pixels / 100 && mean <= 1.5 && percent <= 1.0);
    }

    public static void Save(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
    }
}
