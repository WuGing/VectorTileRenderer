using Mapbox.Vector.Tile;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace VectorTileRenderer.Sources
{
    public class PbfTileSource : IVectorTileSource
    {
        public string Path { get; set; } = "";
        public Stream Stream { get; set; } = null;

        public PbfTileSource(string path)
        {
            Path = path;
        }

        public PbfTileSource(Stream stream)
        {
            Stream = stream;
        }

        public Task<Stream> GetTile(int x, int y, int zoom)
        {
            var qualifiedPath = Path
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString())
                .Replace("{z}", zoom.ToString());
            return Task.FromResult<Stream>(File.Open(qualifiedPath, FileMode.Open, FileAccess.Read, FileShare.Read));
        }
        
        public Task<VectorTile> GetVectorTile(int x, int y, int zoom)
        {
            if (Path != "")
            {
                using var stream = File.Open(
                    Path.Replace("{x}", x.ToString())
                        .Replace("{y}", y.ToString())
                        .Replace("{z}", zoom.ToString()),
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                return Task.FromResult(UnzipStream(stream));
            }
            else if (Stream != null)
            {
                return Task.FromResult(UnzipStream(Stream));
            }

            return Task.FromResult<VectorTile>(null);
        }

        private VectorTile UnzipStream(Stream stream)
        {
            if (PbfTileSource.IsGZipped(stream))
            {
                using var zipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
                return LoadStream(zipStream);
            }
            else
            {
                return LoadStream(stream);
            }
        }
        
        private VectorTile LoadStream(Stream stream)
        {
            var mbLayers = VectorTileParser.Parse(stream);

            return BaseTileToVector(mbLayers);
        }

        private static string ConvertGeometryType(Tile.GeomType type) => type switch
        {
            Tile.GeomType.LineString => "LineString",
            Tile.GeomType.Point => "Point",
            Tile.GeomType.Polygon => "Polygon",
            _ => "Unknown"
        };

        private static VectorTile BaseTileToVector(List<Mapbox.Vector.Tile.VectorTileLayer> baseTile)
        {
            var result = new VectorTile();

            foreach (var lyr in baseTile)
            {
                var vectorLayer = new VectorTileLayer
                {
                    Name = lyr.Name
                };

                foreach (var feat in lyr.VectorTileFeatures)
                {
                    var featureAttributes = new Dictionary<string, object>(feat.Attributes.Count);
                    foreach (var pair in feat.Attributes)
                    {
                        featureAttributes[pair.Key] = pair.Value;
                    }

                    var vectorFeature = new VectorTileFeature
                    {
                        Extent = 1,
                        GeometryType = ConvertGeometryType(feat.GeometryType),
                        Attributes = featureAttributes
                    };

                    var vectorGeometry = new List<List<Point>>();

                    foreach (var points in feat.Geometry)
                    {
                        var pointCountHint = points.Count;
                        var vectorPoints = pointCountHint > 0 ? new List<Point>(pointCountHint) : [];

                        foreach (var coordinate in points)
                        {
                            var dX = coordinate.X / (double)feat.Extent;
                            var dY = coordinate.Y / (double)feat.Extent;

                            vectorPoints.Add(new Point(dX, dY));
                        }

                        vectorGeometry.Add(vectorPoints);
                    }

                    vectorFeature.Geometry = vectorGeometry;
                    vectorLayer.Features.Add(vectorFeature);
                }

                result.Layers.Add(vectorLayer);
            }

            return result;
        }
        
        private static bool IsGZipped(Stream stream)
        {
            return IsZipped(stream, 3, "1F-8B-08");
        }

        private static bool IsZipped(Stream stream, int signatureSize = 4, string expectedSignature = "50-4B-03-04")
        {
            if (stream.Length < signatureSize)
            {   
                return false;
            }

            byte[] signature = new byte[signatureSize];
            int bytesRequired = signatureSize;
            int index = 0;

            while (bytesRequired > 0)
            {
                int bytesRead = stream.Read(signature, index, bytesRequired);
                bytesRequired -= bytesRead;
                index += bytesRead;
            }

            stream.Seek(0, SeekOrigin.Begin);
            string actualSignature = BitConverter.ToString(signature);
            if (actualSignature == expectedSignature) return true;
            return false;
        }
    }
}
