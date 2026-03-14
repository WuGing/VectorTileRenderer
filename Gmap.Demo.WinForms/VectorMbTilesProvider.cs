using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.Projections;
using System;
using SkiaSharp;
using VectorTileRenderer;

namespace Gmap.Demo.WinForms
{
    class VectorMbTilesProvider : GMapProvider
    {
        Style style;
        VectorTileRenderer.Sources.MbTilesSource provider;
        string cachePath;

        public VectorMbTilesProvider(string path, string stylePath, string cachePath)
        {
            style = new Style(stylePath)
            {
                FontDirectory = @"styles/fonts/"
            };
            this.cachePath = cachePath;

            provider = new VectorTileRenderer.Sources.MbTilesSource(path);
            style.SetSourceProvider(0, provider);

            BypassCache = true;
        }

        readonly Guid id = new Guid("36F6CE12-7191-1129-2C48-79DE8C9FB563");
        public override Guid Id
        {
            get
            {
                return id;
            }
        }

        readonly string name = "VectorTileRendererMap";
        public override string Name
        {
            get
            {
                return name;
            }
        }

        public override PureProjection Projection
        {
            get
            {
                return MercatorProjection.Instance;
            }
        }

        GMapProvider[] overlays;
        public override GMapProvider[] Overlays
        {
            get
            {
                if (overlays == null)
                {
                    overlays = new GMapProvider[] { this };
                }
                return overlays;
            }
        }

        public override PureImage GetTileImage(GPoint pos, int zoom)
        {
            var newY = (int)Math.Pow(2, zoom) - pos.Y - 1;

            var canvas = new SkiaCanvas();
            SKBitmap bitmap;

            try
            {
                bitmap = Renderer.RenderCached(cachePath, style, canvas, (int)pos.X, (int)newY, zoom, 256, 256, 1).Result;
            }
            catch (Exception)
            {
                bitmap = null;
            }

            if (bitmap == null)
            {
                bitmap = new SKBitmap(2, 2, SKColorType.Bgra8888, SKAlphaType.Premul);
                using (var canvas2 = new SKCanvas(bitmap))
                {
                    canvas2.Clear(SKColors.Transparent);
                }
            }

            return GetTileImageFromArray(GetBytesFromBitmap(bitmap));
        }

        static byte[] GetBytesFromBitmap(SKBitmap bmp)
        {
            using (var image = SKImage.FromBitmap(bmp))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                return data.ToArray();
            }
        }
    }
}
