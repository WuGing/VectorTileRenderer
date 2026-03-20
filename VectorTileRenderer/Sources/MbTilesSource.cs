using System;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;

namespace WuGing.VectorTileRenderer.Sources;

// MbTiles loading code in GIST by geobabbler
// https://gist.github.com/geobabbler/9213392

public class MbTilesSource : IVectorTileSource
{
    private readonly GlobalMercator gmt = new();
    private readonly string connectionString;
    private readonly ConcurrentBag<SQLiteConnection> connectionPool = [];
    private readonly ConcurrentDictionary<(int X, int Y, int Z), VectorTile> tileCache = new();
    private readonly ConcurrentDictionary<(int X, int Y, int Z), object> tileLocks = new();

    public GlobalMercator.GeoExtent Bounds { get; private set; }
    public GlobalMercator.CoordinatePair Center { get; private set; }
    public int MinZoom { get; private set; }
    public int MaxZoom { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string MBTilesVersion { get; private set; }
    public string Path { get; private set; }

    public MbTilesSource(string path)
    {
        Path = path;
        connectionString = string.Format("Data Source={0};Version=3;Mode=ReadOnly;Pooling=True", Path);

        LoadMetadata();
    }

    private void LoadMetadata()
    {
        try
        {
            var connection = RentConnection();
            try
            {
                using SQLiteCommand cmd = new() { Connection = connection, CommandText = "SELECT * FROM metadata;" };
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string name = reader["name"].ToString();
                    switch (name.ToLower())
                    {
                        case "bounds":
                            string val = reader["value"].ToString();
                            string[] vals = val.Split([',']);
                            Bounds = new GlobalMercator.GeoExtent()
                            {
                                West = Convert.ToDouble(vals[0]),
                                South = Convert.ToDouble(vals[1]),
                                East = Convert.ToDouble(vals[2]),
                                North = Convert.ToDouble(vals[3])
                            };
                            break;
                        case "center":
                            val = reader["value"].ToString();
                            vals = val.Split([',']);
                            Center = new GlobalMercator.CoordinatePair()
                            {
                                X = Convert.ToDouble(vals[0]),
                                Y = Convert.ToDouble(vals[1])
                            };
                            break;
                        case "minzoom":
                            MinZoom = Convert.ToInt32(reader["value"]);
                            break;
                        case "maxzoom":
                            MaxZoom = Convert.ToInt32(reader["value"]);
                            break;
                        case "name":
                            Name = reader["value"].ToString();
                            break;
                        case "description":
                            Description = reader["value"].ToString();
                            break;
                        case "version":
                            MBTilesVersion = reader["value"].ToString();
                            break;

                    }
                }
            }
            finally
            {
                ReturnConnection(connection);
            }
        }
        catch (Exception)
        {
            throw new MemberAccessException("Could not load Mbtiles source file");
        }
    }

    public Stream GetRawTile(int x, int y, int zoom)
    {
        try
        {
            var connection = RentConnection();
            try
            {
                using SQLiteCommand cmd = new("SELECT tile_data FROM tiles WHERE tile_column = @x AND tile_row = @y AND zoom_level = @z", connection);
                cmd.Parameters.AddWithValue("@x", x);
                cmd.Parameters.AddWithValue("@y", y);
                cmd.Parameters.AddWithValue("@z", zoom);

                using SQLiteDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var rawBytes = (byte[])reader["tile_data"];
                    return new MemoryStream(rawBytes, writable: false);
                }
            }
            finally
            {
                ReturnConnection(connection);
            }
        }
        catch
        {
            throw new MemberAccessException("Could not load tile from Mbtiles");
        }

        return null;
    }

    public void ExtractTile(int x, int y, int zoom, string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using var fileStream = File.Create(path);
        using Stream tileStream = GetRawTile(x, y, zoom);
        tileStream.Seek(0, SeekOrigin.Begin);
        tileStream.CopyTo(fileStream);
    }

    public Task<VectorTile> GetVectorTile(int x, int y, int zoom)
    {
        var extent = new Rect(0, 0, 1, 1);
        bool overZoomed = false;

        if (zoom > MaxZoom)
        {
            var bounds = gmt.TileLatLonBounds(x, y, zoom);

            var northEast = new GlobalMercator.CoordinatePair
            {
                X = bounds.East,
                Y = bounds.North
            };

            var northWest = new GlobalMercator.CoordinatePair
            {
                X = bounds.West,
                Y = bounds.North
            };

            var southEast = new GlobalMercator.CoordinatePair
            {
                X = bounds.East,
                Y = bounds.South
            };

            var southWest = new GlobalMercator.CoordinatePair
            {
                X = bounds.West,
                Y = bounds.South
            };

            var center = new GlobalMercator.CoordinatePair
            {
                X = (northEast.X + southWest.X) / 2,
                Y = (northEast.Y + southWest.Y) / 2
            };

            var biggerTile = gmt.LatLonToTile(center.Y, center.X, MaxZoom);

            var biggerBounds = gmt.TileLatLonBounds(biggerTile.X, biggerTile.Y, MaxZoom);

            var newL = Utils.ConvertRange(northWest.X, biggerBounds.West, biggerBounds.East, 0, 1);
            var newT = Utils.ConvertRange(northWest.Y, biggerBounds.North, biggerBounds.South, 0, 1);

            var newR = Utils.ConvertRange(southEast.X, biggerBounds.West, biggerBounds.East, 0, 1);
            var newB = Utils.ConvertRange(southEast.Y, biggerBounds.North, biggerBounds.South, 0, 1);

            extent = new Rect(new Point(newL, newT), new Point(newR, newB));

            x = biggerTile.X;
            y = biggerTile.Y;
            zoom = MaxZoom;

            overZoomed = true;
        }
        
        try
        {
            var actualTile = GetCachedVectorTile(x, y, zoom);

            if (actualTile == null)
            {
                return Task.FromResult<VectorTile>(null);
            }

            if (!overZoomed)
            {
                return Task.FromResult(actualTile);
            }

            var extentTile = actualTile.ApplyExtent(extent);
            extentTile.IsOverZoomed = true;

            return Task.FromResult(extentTile);
        } 
        catch (Exception)
        {
            return Task.FromResult<VectorTile>(null);
        }
    }

    private VectorTile GetCachedVectorTile(int x, int y, int zoom)
    {
        var key = (x, y, zoom);

        var keyLock = tileLocks.GetOrAdd(key, _ => new object());

        lock (keyLock)
        {
            if (tileCache.TryGetValue(key, out var cachedTile))
            {
                return cachedTile;
            }

            using var rawTileStream = GetRawTile(x, y, zoom);
            if (rawTileStream == null)
            {
                return null;
            }

            var pbfTileProvider = new PbfTileSource(rawTileStream);
            var tile = pbfTileProvider.GetVectorTile(x, y, zoom).GetAwaiter().GetResult();
            tileCache[key] = tile;

            return tile;
        }
    }

    Task<Stream> ITileSource.GetTile(int x, int y, int zoom)
    {
        return Task.FromResult(GetRawTile(x, y, zoom));
    }

    private SQLiteConnection RentConnection()
    {
        if (connectionPool.TryTake(out var pooledConnection))
        {
            if (pooledConnection.State != System.Data.ConnectionState.Open)
            {
                pooledConnection.Open();
            }

            return pooledConnection;
        }

        var connection = new SQLiteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private void ReturnConnection(SQLiteConnection connection)
    {
        if (connection == null)
        {
            return;
        }

        if (connection.State == System.Data.ConnectionState.Broken)
        {
            connection.Dispose();
            return;
        }

        connectionPool.Add(connection);
    }
}