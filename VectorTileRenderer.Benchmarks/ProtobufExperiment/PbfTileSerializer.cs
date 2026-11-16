using Mapbox.Vector.Tile;
using ProtoBuf;
using ProtoBuf.Serializers;

namespace VectorTileRenderer.Benchmarks;

internal sealed class PbfTileSerializer : ISerializer<Tile>
{
    public PbfTileSerializer() { }

    public SerializerFeatures Features => SerializerFeatures.CategoryMessage | SerializerFeatures.WireTypeString;

    public Tile Read(ref ProtoReader.State state, Tile value)
    {
        value ??= new Tile();
        int field;
        while ((field = state.ReadFieldHeader()) > 0)
        {
            if (field == 3) value.Layers.Add(state.ReadMessage<Tile.Layer>(null));
            else state.AppendExtensionData(value);
        }
        return value;
    }

    public void Write(ref ProtoWriter.State state, Tile value)
    {
        throw new NotSupportedException("The private PBF model supports reading only.");
    }
}
