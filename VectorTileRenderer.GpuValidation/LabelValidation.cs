using System.Text.Json;
using SkiaSharp;
using WuGing.VectorTileRenderer.Sources;

namespace WuGing.VectorTileRenderer.GpuValidation;

internal static class LabelValidation
{
    public static int Run(string[] args)
    {
        var root = Path.GetFullPath(args.ElementAtOrDefault(0) ?? ".");
        var output = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/label-validation");
        Directory.CreateDirectory(output);
        var records = new List<object>();
        using var host = new WindowsGlContext();
        using var context = GRContext.CreateGl() ?? throw new NotSupportedException("Hardware context required");
        if (!host.IsHardwarePixelFormat) throw new NotSupportedException("Hardware context required");
        using var pump = new RenderThread();
        using var source = new SingleMbTilesSource(Path.Combine(root, "tiles/zurich.mbtiles"));
        foreach (var styleName in new[] { "basic", "bright" })
        foreach (var zoom in new[] { 12, 14, 16, 18 })
        foreach (var backend in new[] { "cpu", "gpu" })
        {
            var latitude = args.Length > 2 ? double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 47.368659;
            var longitude = args.Length > 3 ? double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 8.542693;
            var center = new GlobalMercator().LatLonToTile(latitude, longitude, zoom);
            var style = new Style(Path.Combine(root, $"styles/{styleName}-style.json"))
                { FontDirectory = Path.Combine(root, "styles/fonts") };
            style.SetSourceProvider(0, source);
            using var probe = new ProbeCanvas(backend == "gpu" ? context : null) { VerifyReadback = true };
            using var sheet = new SKBitmap(768, 768);
            using var compose = new SKCanvas(sheet);
            compose.Clear(SKColors.Transparent);
            for (var row = 0; row < 3; row++)
            for (var column = 0; column < 3; column++)
            {
                var x = (int)center.X + column - 1;
                var y = (int)center.Y + 1 - row;
                var tile = pump.Run(() => Renderer.Render(style, probe, x, y, zoom, 256, 256));
                if (tile != null)
                {
                    if (backend == "gpu" && (!probe.IsGpuEnabled || !probe.TextureBacked || probe.ReadbackSucceeded != true))
                        throw new InvalidOperationException("GPU label readback failed");
                    compose.DrawBitmap(tile, column * 256, row * 256);
                }
                records.Add(new { Style = styleName, Zoom = zoom, Backend = backend, X = x, TmsY = y, Rendered = tile != null });
            }
            PixelComparison.Save(sheet, Path.Combine(output, $"zurich-{styleName}-z{zoom}-{backend}.png"));
            Console.WriteLine($"Rendered {styleName} z{zoom} {backend}");
        }
        foreach (var backend in new[] { "cpu", "gpu" })
        {
            using var probe = new ProbeCanvas(backend == "gpu" ? context : null) { VerifyReadback = true };
            probe.StartDrawing(768, 512);
            probe.DrawBackground(new Brush { Paint = new Paint { BackgroundColor = Color.FromRgb(255, 255, 255) } });
            var labels = new[] { "Main Street", "Main \u0416 \u03a9", "a\u0301 \U0001F680 tail", "\u0633\u0644\u0627\u0645", "A \U0001F680 B" };
            for (var i = 0; i < labels.Length; i++)
            {
                probe.DrawText(new Point(384, 45 + i * 65), new Brush
                {
                    Text = labels[i], GlyphsDirectory = Path.Combine(root, "styles/fonts"),
                    Paint = new Paint { TextFont = ["Metropolis Regular", "OpenSans Regular"], TextSize = 28,
                        TextMaxWidth = 30, TextColor = Color.FromArgb(255, 0, 0, 0) }
                });
            }
            probe.DrawTextOnPath([new(100, 420), new(380, 405), new(650, 420)], new Brush
            {
                Text = "Main \u0416 \u03a9", GlyphsDirectory = Path.Combine(root, "styles/fonts"),
                Paint = new Paint { TextFont = ["Metropolis Regular", "OpenSans Regular"], TextSize = 28,
                    TextColor = Color.FromArgb(255, 0, 0, 0) }
            });
            var outputBitmap = probe.FinishDrawing();
            if (backend == "gpu" && (!probe.IsGpuEnabled || !probe.TextureBacked || probe.ReadbackSucceeded != true))
                throw new InvalidOperationException("Unicode GPU readback failed.");
            PixelComparison.Save(outputBitmap, Path.Combine(output, "unicode-" + backend + ".png"));
        }
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { host.Renderer, host.Version,
            TimestampUtc = DateTime.UtcNow, Tiles = records }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
