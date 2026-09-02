namespace WuGing.VectorTileRenderer.Sources;

public class RasterTileSource(string path) : ITileSource
{
    public string Path { get; private set; } = path;

    public Task<Stream> GetTile(int x, int y, int z)
    {
        var qualifiedPath = Path
            .Replace("{x}", x.ToString())
            .Replace("{y}", y.ToString())
            .Replace("{z}", z.ToString());

        return Task.FromResult<Stream>(File.Open(qualifiedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
    }
}
