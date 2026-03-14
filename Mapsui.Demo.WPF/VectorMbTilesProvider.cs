using BruTile;
using SkiaSharp;
using System;
using VectorTileRenderer;

namespace Mapsui.Demo.WPF
{
    class VectorMbTilesProvider : ITileProvider
    {
        Style style;
        VectorTileRenderer.Sources.MbTilesSource provider;
        string cachePath;

        public VectorMbTilesProvider(string path, string stylePath, string cachePath)
        {
            this.cachePath = cachePath;
            style = new Style(stylePath)
            {
                FontDirectory = @"styles/fonts/"
            };

            provider = new VectorTileRenderer.Sources.MbTilesSource(path);
            style.SetSourceProvider("openmaptiles", provider);
        }

        public byte[] GetTile(TileInfo tileInfo)
        {
            var canvas = new SkiaCanvas();
            SKBitmap bitmap;

            try
            {
                bitmap = Renderer.RenderCached(cachePath, style, canvas, (int)tileInfo.Index.Col, (int)tileInfo.Index.Row, Convert.ToInt32(tileInfo.Index.Level), 256, 256, 1).Result;
            }
            catch
            {
                return null;
            }

            return GetBytesFromBitmap(bitmap);
        }

        static byte[] GetBytesFromBitmap(SKBitmap bmp)
        {
            if (bmp == null)
            {
                return null;
            }

            using (var image = SKImage.FromBitmap(bmp))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                return data.ToArray();
            }
        }
    }
}
