using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Benchmarks;

internal static class DecodeSnapshots
{
    public static void Run(string root, string output)
    {
        root = Path.GetFullPath(root);
        var rows = new List<object>();
        foreach (var file in new[] { "zurich.pbf.gz", "newyork-mapbox.pbf" })
        {
            var source = new PbfTileSource(Path.Combine(root, "tiles", file));
            rows.Add(Snapshot(file, source.GetVectorTile(0, 0, 0).GetAwaiter().GetResult()));
        }
        using var native = new SingleMbTilesSource(Path.Combine(root, "tiles", "zurich.mbtiles"));
        foreach (var item in new[] { (Z: 12, X: 2145, Y: 2662), (Z: 14, X: 8580, Y: 10646) })
            rows.Add(Snapshot($"zurich-{item.Z}-{item.X}-{item.Y}", native.GetVectorTile(item.X, item.Y, item.Z).GetAwaiter().GetResult()));
        File.WriteAllText(output, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(output);
    }

    private static object Snapshot(string name, VectorTile tile)
    {
        if (tile == null) throw new InvalidOperationException("Snapshot tile missing.");
        var content = new
        {
            tile.IsOverZoomed,
            Layers = tile.Layers.Select(layer => new
            {
                layer.Name,
                Features = layer.Features.Select(feature => new
                {
                    feature.Extent, feature.GeometryType,
                    Attributes = feature.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => new { pair.Key, Type = pair.Value?.GetType().FullName, pair.Value }),
                    Geometry = feature.Geometry.Select(part => part.Select(point => new[]
                        { BitConverter.DoubleToInt64Bits(point.X), BitConverter.DoubleToInt64Bits(point.Y) }))
                })
            })
        };
        using var hash = SHA256.Create();
        return new
        {
            Case = name, Layers = tile.Layers.Count, Features = tile.Layers.Sum(layer => layer.Features.Count),
            Points = tile.Layers.Sum(layer => layer.Features.Sum(feature => feature.Geometry.Sum(part => part.Count))),
            Sha256 = Convert.ToHexString(hash.ComputeHash(JsonSerializer.SerializeToUtf8Bytes(content)))
        };
    }
}
