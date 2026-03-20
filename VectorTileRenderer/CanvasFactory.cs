namespace WuGing.VectorTileRenderer;

public enum RenderBackend
{
    Cpu,
    Gpu,
    Auto,
}

public static class CanvasFactory
{
    private static readonly object backendProbeLock = new();
    private static bool backendProbed;
    private static bool gpuAvailable;

    public static ICanvas Create(RenderBackend backend = RenderBackend.Auto)
    {
        if (backend == RenderBackend.Cpu)
        {
            return new SkiaCanvas();
        }

        if (backend == RenderBackend.Auto)
        {
            lock (backendProbeLock)
            {
                if (!backendProbed)
                {
                    var probe = SkiaGpuCanvas.TryCreate();
                    gpuAvailable = probe.IsGpuEnabled;
                    backendProbed = true;

                    if (gpuAvailable)
                    {
                        return probe;
                    }

                    return new SkiaCanvas();
                }

                if (!gpuAvailable)
                {
                    return new SkiaCanvas();
                }
            }
        }

        var gpuCanvas = SkiaGpuCanvas.TryCreate();
        if (gpuCanvas.IsGpuEnabled)
        {
            return gpuCanvas;
        }

        return new SkiaCanvas();
    }
}