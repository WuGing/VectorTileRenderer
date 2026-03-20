using Mapbox.Vector.Tile;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

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

    public Task<Stream> GetTile(int x, int y, int zoom)
    {
        var qualifiedPath = ResolveTilePath(Path, x, y, zoom);
        return Task.FromResult<Stream>(File.Open(qualifiedPath, FileMode.Open, FileAccess.Read, FileShare.Read));
    }
    
    public Task<VectorTile> GetVectorTile(int x, int y, int zoom)
    {
        if (Path != string.Empty)
        {
            var qualifiedPath = ResolveTilePath(Path, x, y, zoom);
            using var stream = File.Open(qualifiedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult(UnzipStream(stream));
        }
        else if (Stream is not null)
        {
            return Task.FromResult(UnzipStream(Stream));
        }

        return Task.FromResult<VectorTile>(null);
    }

    private static string ResolveTilePath(string templatePath, int x, int y, int zoom)
    {
        if (string.IsNullOrEmpty(templatePath) || templatePath.IndexOf('{') < 0)
        {
            return templatePath;
        }

        var builder = new StringBuilder(templatePath.Length + 8);
        string xString = null;
        string yString = null;
        string zString = null;

        for (int i = 0; i < templatePath.Length; i++)
        {
            var c = templatePath[i];
            if (c != '{')
            {
                builder.Append(c);
                continue;
            }

            var closeIndex = templatePath.IndexOf('}', i + 1);
            if (closeIndex < 0)
            {
                builder.Append(c);
                continue;
            }

            var token = templatePath.Substring(i + 1, closeIndex - i - 1);
            switch (token)
            {
                case "x":
                    xString ??= x.ToString();
                    builder.Append(xString);
                    break;
                case "y":
                    yString ??= y.ToString();
                    builder.Append(yString);
                    break;
                case "z":
                    zString ??= zoom.ToString();
                    builder.Append(zString);
                    break;
                default:
                    builder.Append('{').Append(token).Append('}');
                    break;
            }

            i = closeIndex;
        }

        return builder.ToString();
    }

    private VectorTile UnzipStream(Stream stream)
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