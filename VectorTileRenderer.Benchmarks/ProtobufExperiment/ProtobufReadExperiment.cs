using System.Globalization;
using Mapbox.Vector.Tile;
using ProtoBuf;
using ProtoBuf.Meta;

namespace VectorTileRenderer.Benchmarks;

// Adapted from mapbox-vector-tile 5.3.0. See THIRD-PARTY-NOTICES.txt.
// Isolated experiment. Match the production adapter's decoded-model construction
// so the comparison measures the custom protobuf read model.
internal static class ProtobufReadExperiment
{
    private static readonly RuntimeTypeModel model = CreateModel();

    private static RuntimeTypeModel CreateModel()
    {
        var result = RuntimeTypeModel.Create();
        result.Add(typeof(Tile), false).SerializerType = typeof(PbfTileSerializer);
        result.Add(typeof(Tile.Layer), false).SerializerType = typeof(PbfLayerSerializer);
        result.Add(typeof(Tile.Feature), false).SerializerType = typeof(PbfFeatureSerializer);
        return result;
    }

    public static List<Mapbox.Vector.Tile.VectorTileLayer> Parse(Stream stream)
    {
        var tile = ReadTile(stream);
        var result = new List<Mapbox.Vector.Tile.VectorTileLayer>(tile.Layers.Count);
        foreach (var layer in tile.Layers)
        {
            var decoded = new Mapbox.Vector.Tile.VectorTileLayer(layer.Name ?? "[Unnamed]", layer.Version, layer.Extent);
            decoded.VectorTileFeatures.Capacity = layer.Features.Count;
            foreach (var feature in layer.Features)
            {
                // Match FeatureParser's geometry-before-attributes evaluation order.
                var geometry = GeometryParser.ParseGeometry(feature.Geometry, feature.Type);
                var tags = feature.Tags;
                var attributes = new List<KeyValuePair<string, object>>(tags.Count / 2);
                for (var i = 0; i < tags.Count; i += 2)
                {
                    var key = layer.Keys[(int)tags[i]];
                    var value = layer.Values[(int)tags[i + 1]];
                    attributes.Add(new KeyValuePair<string, object>(key, GetAttributeValue(value)));
                }
                decoded.VectorTileFeatures.Add(new Mapbox.Vector.Tile.VectorTileFeature(
                    feature.Id.ToString(CultureInfo.InvariantCulture), geometry, attributes, feature.Type, layer.Extent));
            }
            result.Add(decoded);
        }
        return result;
    }

    internal static Tile ReadTile(Stream stream) => model.Deserialize<Tile>(stream);

    private static object GetAttributeValue(Tile.Value value)
    {
        // Preserve the dependency's precedence, including values with multiple fields.
        if (value.HasBoolValue) return value.BoolValue;
        if (value.HasDoubleValue) return value.DoubleValue;
        if (value.HasFloatValue) return value.FloatValue;
        if (value.HasIntValue) return value.IntValue;
        if (value.HasStringValue) return value.StringValue;
        if (value.HasSIntValue) return value.SintValue;
        if (value.HasUIntValue) return value.UintValue;
        throw new NotImplementedException("Unknown attribute type");
    }
}
