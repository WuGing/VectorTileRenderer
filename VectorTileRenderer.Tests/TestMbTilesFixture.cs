using System.Data.SQLite;

namespace VectorTileRenderer.Tests;

internal sealed class TestMbTilesFixture : IDisposable
{
    private static int staleFixturesCleaned;

    private TestMbTilesFixture(
        string path,
        string name,
        GlobalMercator.GeoExtent bounds,
        double latitude,
        double longitude,
        int zoom,
        byte[] tileData)
    {
        Path = path;
        Name = name;
        Bounds = bounds;
        Latitude = latitude;
        Longitude = longitude;
        Zoom = zoom;
        TileData = tileData;

        var coordinate = new GlobalMercator().LatLonToTile(latitude, longitude, zoom);
        TileX = coordinate.X;
        TileY = coordinate.Y;

        SQLiteConnection.CreateFile(path);
        using var connection = new SQLiteConnection($"Data Source={path};Version=3;");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE metadata (name TEXT NOT NULL, value TEXT NOT NULL);
            CREATE TABLE tiles (
                zoom_level INTEGER NOT NULL,
                tile_column INTEGER NOT NULL,
                tile_row INTEGER NOT NULL,
                tile_data BLOB NOT NULL,
                PRIMARY KEY (zoom_level, tile_column, tile_row)
            );
            """;
        command.ExecuteNonQuery();

        InsertMetadata(connection, "name", name);
        InsertMetadata(connection, "description", $"Synthetic test fixture {name}");
        InsertMetadata(connection, "version", "1.0");
        InsertMetadata(connection, "format", "pbf");
        InsertMetadata(connection, "bounds", FormattableString.Invariant(
            $"{bounds.West},{bounds.South},{bounds.East},{bounds.North}"));
        InsertMetadata(connection, "center", FormattableString.Invariant($"{longitude},{latitude},{zoom}"));
        InsertMetadata(connection, "minzoom", zoom.ToString(System.Globalization.CultureInfo.InvariantCulture));
        InsertMetadata(connection, "maxzoom", zoom.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var tileCommand = connection.CreateCommand();
        tileCommand.CommandText = """
            INSERT INTO tiles (zoom_level, tile_column, tile_row, tile_data)
            VALUES (@zoom, @x, @y, @data);
            """;
        tileCommand.Parameters.AddWithValue("@zoom", zoom);
        tileCommand.Parameters.AddWithValue("@x", TileX);
        tileCommand.Parameters.AddWithValue("@y", TileY);
        tileCommand.Parameters.AddWithValue("@data", tileData);
        tileCommand.ExecuteNonQuery();
    }

    public string Path { get; }
    public string Name { get; }
    public GlobalMercator.GeoExtent Bounds { get; }
    public double Latitude { get; }
    public double Longitude { get; }
    public int Zoom { get; }
    public int TileX { get; }
    public int TileY { get; }
    public byte[] TileData { get; }

    public static TestMbTilesFixture Create(
        string name,
        GlobalMercator.GeoExtent bounds,
        double latitude,
        double longitude,
        int zoom = 10,
        uint featureId = 1)
    {
        CleanStaleFixturesOnce();
        var path = System.IO.Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"synthetic-{name}-{Guid.NewGuid():N}.mbtiles");
        var tileData = CreatePointTile(name, featureId);
        return new TestMbTilesFixture(path, name, bounds, latitude, longitude, zoom, tileData);
    }

    public void Dispose()
    {
        SQLiteConnection.ClearAllPools();
        try
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
        catch (IOException)
        {
            // System.Data.SQLite can retain a Windows native handle until the
            // test process exits. The next run removes stale fixture files.
        }
    }

    private static void CleanStaleFixturesOnce()
    {
        if (System.Threading.Interlocked.Exchange(ref staleFixturesCleaned, 1) != 0)
        {
            return;
        }

        var workDirectory = TestContext.CurrentContext.WorkDirectory;
        foreach (var path in Directory.EnumerateFiles(workDirectory, "synthetic-*.mbtiles"))
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A concurrent test process may still own the file.
            }
        }
    }

    private static void InsertMetadata(SQLiteConnection connection, string name, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO metadata (name, value) VALUES (@name, @value);";
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@value", value);
        command.ExecuteNonQuery();
    }

    private static byte[] CreatePointTile(string layerName, uint featureId)
    {
        using var feature = new MemoryStream();
        WriteVarintField(feature, 1, featureId);
        WriteVarintField(feature, 3, 1);
        WriteLengthDelimitedField(feature, 4, CreatePackedVarints(9, 4096, 4096));

        using var layer = new MemoryStream();
        WriteStringField(layer, 1, layerName);
        WriteLengthDelimitedField(layer, 2, feature.ToArray());
        WriteVarintField(layer, 5, 4096);
        WriteVarintField(layer, 15, 2);

        using var tile = new MemoryStream();
        WriteLengthDelimitedField(tile, 3, layer.ToArray());
        return tile.ToArray();
    }

    private static byte[] CreatePackedVarints(params uint[] values)
    {
        using var stream = new MemoryStream();
        foreach (var value in values)
        {
            WriteVarint(stream, value);
        }

        return stream.ToArray();
    }

    private static void WriteStringField(Stream stream, uint fieldNumber, string value)
    {
        WriteLengthDelimitedField(stream, fieldNumber, System.Text.Encoding.UTF8.GetBytes(value));
    }

    private static void WriteVarintField(Stream stream, uint fieldNumber, uint value)
    {
        WriteVarint(stream, fieldNumber << 3);
        WriteVarint(stream, value);
    }

    private static void WriteLengthDelimitedField(Stream stream, uint fieldNumber, byte[] value)
    {
        WriteVarint(stream, (fieldNumber << 3) | 2);
        WriteVarint(stream, (uint)value.Length);
        stream.Write(value, 0, value.Length);
    }

    private static void WriteVarint(Stream stream, uint value)
    {
        while (value >= 0x80)
        {
            stream.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }

        stream.WriteByte((byte)value);
    }
}
