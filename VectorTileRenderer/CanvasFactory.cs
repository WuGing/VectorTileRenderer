namespace WuGing.VectorTileRenderer;

public enum RenderBackend
{
    Cpu,
    Gpu,
    Auto,
}

public static class CanvasFactory
{
    public static ICanvas Create(RenderBackend backend = RenderBackend.Auto)
    {
        if (backend == RenderBackend.Cpu)
        {
            return new SkiaCanvas();
        }

        // GL availability belongs to the calling thread's current native context.
        // A failed probe must not disable GPU selection for later calls or hosts.
        var gpuCanvas = SkiaGpuCanvas.TryCreate();
        if (gpuCanvas.IsGpuEnabled)
        {
            return gpuCanvas;
        }

        gpuCanvas.Dispose();
        return new SkiaCanvas();
    }
}
