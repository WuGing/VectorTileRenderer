using Mapbox.Vector.Tile;
using ProtoBuf;
using ProtoBuf.Serializers;

namespace VectorTileRenderer.Benchmarks;

internal sealed class PbfLayerSerializer : ISerializer<Tile.Layer>
{
    public PbfLayerSerializer() { }

    public SerializerFeatures Features => SerializerFeatures.CategoryMessage | SerializerFeatures.WireTypeString;

    public Tile.Layer Read(ref ProtoReader.State state, Tile.Layer value)
    {
        value ??= new Tile.Layer();
        int field;
        while ((field = state.ReadFieldHeader()) > 0)
        {
            switch (field)
            {
                case 1:
                    value.Name = state.ReadString();
                    break;
                case 2:
                    value.Features.Add(state.ReadMessage<Tile.Feature>(null));
                    break;
                case 3:
                    value.Keys.Add(state.ReadString());
                    break;
                case 4:
                    value.Values.Add(state.ReadMessage<Tile.Value>(null));
                    break;
                case 5:
                    value.Extent = state.ReadUInt32();
                    break;
                case 15:
                    value.Version = state.ReadUInt32();
                    break;
                default:
                    state.AppendExtensionData(value);
                    break;
            }
        }
        return value;
    }

    public void Write(ref ProtoWriter.State state, Tile.Layer value)
    {
        throw new NotSupportedException("The private PBF model supports reading only.");
    }
}
