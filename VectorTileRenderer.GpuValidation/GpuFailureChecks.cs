using SkiaSharp;

namespace WuGing.VectorTileRenderer.GpuValidation;

internal static class GpuFailureChecks
{
    // The caller has already probed Auto without a native context, then bound WGL.
    public static bool Run(Action<string, bool> record)
    {
        var passed = true;
        void Check(string name, bool result)
        {
            record(name, result);
            passed &= result;
        }

        using (var context = GRContext.CreateGl() ?? throw new InvalidOperationException("Context creation failed"))
        using (var lost = new ProbeCanvas(context))
        {
            lost.StartDrawing(64, 64);
            Check("Surface exists before abandonment", lost.IsGpuEnabled);
            context.AbandonContext();
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var rejected = false;
                try { lost.FinishDrawing(); }
                catch (InvalidOperationException error) when (error.Message.Contains("GPU pixel readback failed"))
                {
                    rejected = true;
                }
                Check($"Failed readback rejects bitmap, attempt {attempt}", rejected);
            }
            lost.StartDrawing(64, 64);
            lost.DrawBackground(Background());
            var fallback = lost.FinishDrawing();
            Check("Abandoned context starts a new correct CPU render",
                !lost.IsGpuEnabled && fallback.GetPixel(0, 0) == new SKColor(20, 40, 60));
        }

        var auto = CanvasFactory.Create(RenderBackend.Auto);
        auto.StartDrawing(64, 64);
        auto.DrawBackground(Background());
        using var image = auto.FinishDrawing();
        Check("Auto recovers after initial no-context failure with GPU pixels",
            auto is SkiaGpuCanvas { IsGpuEnabled: true } && image.GetPixel(0, 0) == new SKColor(20, 40, 60));

        var workerFallback = Task.Run(() =>
        {
            if (WindowsGlContext.HasCurrentContext) return false;
            var worker = CanvasFactory.Create(RenderBackend.Auto);
            worker.StartDrawing(64, 64);
            worker.DrawBackground(Background());
            using var output = worker.FinishDrawing();
            return worker is not SkiaGpuCanvas { IsGpuEnabled: true }
                && output.GetPixel(0, 0) == new SKColor(20, 40, 60);
        }).GetAwaiter().GetResult();
        Check("Auto on context-free worker renders CPU after hosted success", workerFallback);

        var again = CanvasFactory.Create(RenderBackend.Auto);
        again.StartDrawing(64, 64);
        again.DrawBackground(Background());
        using var next = again.FinishDrawing();
        Check("Worker fallback does not poison later hosted Auto",
            again is SkiaGpuCanvas { IsGpuEnabled: true } && next.GetPixel(0, 0) == new SKColor(20, 40, 60));
        return passed;
    }

    private static Brush Background() => new()
    {
        Paint = new Paint { BackgroundColor = Color.FromArgb(255, 20, 40, 60) }
    };
}
