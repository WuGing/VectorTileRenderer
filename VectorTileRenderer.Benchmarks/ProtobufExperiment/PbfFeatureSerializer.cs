using Mapbox.Vector.Tile;
using ProtoBuf;
using ProtoBuf.Serializers;

namespace VectorTileRenderer.Benchmarks;

// Registered only on the experiment's private read model. Append repeated scalars
// directly to their owning lists instead of staging them in pooled read buffers.
internal sealed class PbfFeatureSerializer : ISerializer<Tile.Feature>
{
    public PbfFeatureSerializer() { }

    public SerializerFeatures Features => SerializerFeatures.CategoryMessage | SerializerFeatures.WireTypeString;

    public Tile.Feature Read(ref ProtoReader.State state, Tile.Feature value)
    {
        value ??= new Tile.Feature();
        int field;
        while ((field = state.ReadFieldHeader()) > 0)
        {
            switch (field)
            {
                case 1:
                    value.Id = state.ReadUInt64();
                    break;
                case 2:
                    ReadValues(ref state, value.Tags);
                    break;
                case 3:
                    value.Type = (Tile.GeomType)state.ReadInt32();
                    break;
                case 4:
                    ReadValues(ref state, value.Geometry);
                    break;
                default:
                    state.AppendExtensionData(value);
                    break;
            }
        }
        return value;
    }

    private static void ReadValues(ref ProtoReader.State state, List<uint> values)
    {
        if (state.WireType == WireType.String)
        {
            var token = state.StartSubItem();
            while (state.HasSubValue(WireType.Varint)) values.Add(state.ReadUInt32());
            state.EndSubItem(token);
        }
        else values.Add(state.ReadUInt32());
    }

    public void Write(ref ProtoWriter.State state, Tile.Feature value)
    {
        throw new NotSupportedException("The private PBF model supports reading only.");
    }
}
