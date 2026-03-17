using SkiaSharp;
using System;

namespace VectorTileRenderer
{
    public class SkiaGpuCanvas : SkiaCanvas
    {
        private readonly GRContext grContext;

        public bool IsGpuEnabled { get; private set; }

        public SkiaGpuCanvas(GRContext context)
        {
            grContext = context;
            IsGpuEnabled = context != null;
        }

        public static SkiaGpuCanvas TryCreate()
        {
            try
            {
                var context = GRContext.CreateGl();
                return new SkiaGpuCanvas(context);
            }
            catch
            {
                return new SkiaGpuCanvas(null);
            }
        }

        protected override bool TryCreateSurface(SKImageInfo info, out SKSurface createdSurface, out SKBitmap createdBitmap)
        {
            createdBitmap = null;
            createdSurface = null;

            if (grContext == null)
            {
                IsGpuEnabled = false;
                return false;
            }

            try
            {
                createdBitmap = new SKBitmap(info);
                createdSurface = SKSurface.Create(grContext, true, info);
                IsGpuEnabled = createdSurface != null;

                if (!IsGpuEnabled)
                {
                    createdBitmap.Dispose();
                    createdBitmap = null;
                    return false;
                }

                return true;
            }
            catch
            {
                IsGpuEnabled = false;
                createdBitmap?.Dispose();
                createdBitmap = null;
                createdSurface = null;
                return false;
            }
        }

        protected override void OnBeforeFinishDrawing()
        {
            if (!IsGpuEnabled || surface == null || bitmap == null)
            {
                return;
            }

            using (var image = surface.Snapshot())
            {
                if (image == null)
                {
                    return;
                }

                image.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);
            }
        }
    }
}
