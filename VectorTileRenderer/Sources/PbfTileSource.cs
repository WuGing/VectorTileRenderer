using Mapbox.Vector.Tile;
using System.IO.Compression;

namespace WuGing.VectorTileRenderer.Sources;

public class PbfTileSource : IVectorTileSource
{
    public string Path { get; init; } = string.Empty;
    public Stream Stream { get; init; } = null;

    public PbfTileSource(string path)
    {
        Path = path;
    }

    public PbfTileSource(Stream stream)
    {
        Stream = stream;
    }

    public Task<Stream> GetTile(int x, int y, int z)
    {
        var qualifiedPath = TilePathResolver.Resolve(Path, x, y, z);
        return Task.FromResult<Stream>(File.Open(qualifiedPath, FileMode.Open, FileAccess.Read, FileShare.Read));
    }
    
    public Task<VectorTile> GetVectorTile(int x, int y, int z)
    {
        if (Path != string.Empty)
        {
            var qualifiedPath = TilePathResolver.Resolve(Path, x, y, z);
            using var stream = File.Open(qualifiedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult(UnzipStream(stream));
        }
        else if (Stream is not null)
        {
            return Task.FromResult(UnzipStream(Stream));
        }

        return Task.FromResult<VectorTile>(null);
    }

    private static VectorTile UnzipStream(Stream stream)
    {
        if (IsGZipped(stream))
        {
            using var zipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            return LoadStream(zipStream);
        }
        else
        {
            return LoadStream(stream);
        }
    }
    
    private static VectorTile LoadStream(Stream stream)
    {
        return PbfParser.ParseRendererTile(stream);
    }

    internal static string ConvertGeometryType(Tile.GeomType type) => type switch
    {
        Tile.GeomType.LineString => "LineString",
        Tile.GeomType.Point => "Point",
        Tile.GeomType.Polygon => "Polygon",
        _ => "Unknown"
    };

    private static VectorTile BaseTileToVector(List<Mapbox.Vector.Tile.VectorTileLayer> baseTile)
    {
        var result = new VectorTile();
        result.Layers.Capacity = baseTile.Count;

        foreach (var lyr in baseTile)
        {
            var vectorLayer = new VectorTileLayer
            {
                Name = lyr.Name
            };
            vectorLayer.Features.Capacity = lyr.VectorTileFeatures.Count;

            foreach (var feat in lyr.VectorTileFeatures)
            {
                var vectorFeature = new VectorTileFeature(feat.Attributes.Count, feat.Geometry.Count)
                {
                    Extent = 1,
                    GeometryType = ConvertGeometryType(feat.GeometryType)
                };
                foreach (var pair in feat.Attributes)
                {
                    vectorFeature.Attributes[pair.Key] = pair.Value;
                }

                AddGeometry(vectorFeature, feat.Geometry, feat.Extent);
                vectorLayer.Features.Add(vectorFeature);
            }

            result.Layers.Add(vectorLayer);
        }

        return result;
    }

    internal static void AddGeometry(VectorTileFeature feature, List<ArraySegment<Coordinate>> geometry, uint extent)
    {
        foreach (var points in geometry)
        {
            var vectorPoints = points.Count > 0 ? new List<Point>(points.Count) : [];
            foreach (var coordinate in points)
            {
                var dX = coordinate.X / (double)extent;
                var dY = coordinate.Y / (double)extent;
                vectorPoints.Add(new Point(dX, dY));
            }
            feature.Geometry.Add(vectorPoints);
        }
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
