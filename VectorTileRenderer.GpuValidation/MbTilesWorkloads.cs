using System.Data.SQLite;
using WuGing.VectorTileRenderer.Sources;

namespace WuGing.VectorTileRenderer.GpuValidation;

// Selects demanding local tiles without copying or modifying the database.
internal sealed class MbTilesWorkloads : IDisposable
{
    private readonly List<SingleMbTilesSource> ownedSources = [];
    public List<Workload> Workloads { get; } = [];
    public List<object> Evidence { get; } = [];

    public MbTilesWorkloads(string path, string root)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Local MBTiles dataset not found", path);
        try
        {
            using var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder
            {
                DataSource = path, ReadOnly = true, FailIfMissing = true
            }.ConnectionString);
            connection.Open();
            var metadata = new Dictionary<string, string>();
            using (var command = new SQLiteCommand("SELECT name, value FROM metadata", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) metadata[reader.GetString(0)] = reader.GetString(1);
            }
            if (metadata.GetValueOrDefault("format") != "pbf" || metadata.GetValueOrDefault("scheme", "tms") != "tms")
            {
                throw new NotSupportedException("This experiment requires vector PBF with MBTiles TMS rows.");
            }
            var maximum = int.Parse(metadata["maxzoom"], System.Globalization.CultureInfo.InvariantCulture);
            Evidence.Add(new { Case = "LocalDataset", File = Path.GetFileName(path), Bytes = new FileInfo(path).Length,
                Metadata = metadata.Where(pair => pair.Key != "json").ToDictionary(),
                Selection = "Largest compressed tile at each selected native zoom, ties resolved by column/row; stress sample, not whole-database coverage" });
            foreach (var zoom in new[] { Math.Min(10, maximum), Math.Min(12, maximum), maximum }.Distinct())
            {
                int x, y, bytes;
                using (var command = new SQLiteCommand("""
                    SELECT tile_column, tile_row, length(tile_data)
                    FROM tiles WHERE zoom_level = @zoom
                    ORDER BY length(tile_data) DESC, tile_column, tile_row LIMIT 1
                    """, connection))
                {
                    command.Parameters.AddWithValue("@zoom", zoom);
                    using var reader = command.ExecuteReader();
                    if (!reader.Read()) continue;
                    x = reader.GetInt32(0);
                    y = reader.GetInt32(1);
                    bytes = reader.GetInt32(2);
                }
                var warm = new SingleMbTilesSource(path);
                ownedSources.Add(warm);
                var tile = warm.GetVectorTile(x, y, zoom).GetAwaiter().GetResult()
                    ?? throw new InvalidOperationException($"Production provider could not decode {zoom}/{x}/{y}.");
                Evidence.Add(new { Case = "SelectedTile", X = x, TmsY = y, Zoom = zoom, CompressedBytes = bytes,
                    FeatureCount = tile.Layers.Sum(layer => layer.Features.Count),
                    PointCount = tile.Layers.Sum(layer => layer.Features.Sum(feature => feature.Geometry.Sum(part => part.Count))),
                    Layers = tile.Layers.Select(layer => new { layer.Name, Features = layer.Features.Count }).ToArray() });
                foreach (var fresh in new[] { false, true })
                {
                    IVectorTileSource source = fresh ? new FreshSource(path) : warm;
                    var style = new Style(Path.Combine(root, "styles", "basic-style.json"))
                    {
                        FontDirectory = Path.Combine(root, "styles", "fonts")
                    };
                    style.SetSourceProvider(0, source);
                    Workloads.Add(new Workload($"mbtiles-z{zoom}-{x}-{y}-{(fresh ? "fresh-source" : "warm-source")}",
                        style, source, x, y, zoom));
                }
            }
            if (Workloads.Count == 0) throw new InvalidOperationException("No tiles selected.");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var source in ownedSources) source.Dispose();
    }

    // Includes metadata and a fresh decoded-tile cache each request. OS/SQLite caches remain uncontrolled.
    private sealed class FreshSource(string path) : IVectorTileSource
    {
        public Task<Stream> GetTile(int x, int y, int z) => throw new NotSupportedException();
        public async Task<VectorTile> GetVectorTile(int x, int y, int z)
        {
            using var source = new SingleMbTilesSource(path);
            return await source.GetVectorTile(x, y, z);
        }
    }
}
