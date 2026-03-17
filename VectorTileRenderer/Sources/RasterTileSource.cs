using System.IO;
using System.Threading.Tasks;

namespace VectorTileRenderer.Sources;

public class RasterTileSource(string path) : ITileSource
{
    public string Path { get; private set; } = path;

    public Task<Stream> GetTile(int x, int y, int zoom)
    {
        var qualifiedPath = Path
            .Replace("{x}", x.ToString())
            .Replace("{y}", y.ToString())
            .Replace("{z}", zoom.ToString());

        return Task.FromResult<Stream>(File.Open(qualifiedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
    }
}