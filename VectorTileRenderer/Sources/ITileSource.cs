namespace WuGing.VectorTileRenderer.Sources;

public interface ITileSource
{
    Task<Stream> GetTile(int x, int y, int z);
}