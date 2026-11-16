using System.Globalization;
using Mapbox.Vector.Tile;
using ProtoBuf;

namespace WuGing.VectorTileRenderer.Sources;

// Adapted from mapbox-vector-tile 5.3.0. See THIRD-PARTY-NOTICES.txt.
// Keep the dependency's protobuf contracts and geometry decoding while avoiding
// temporary odd/even tag lists and repeated growth of the decoded collections.
internal static class PbfParser
{
    public static VectorTile ParseRendererTile(Stream stream)
    {
        var tile = Serializer.Deserialize<Tile>(stream);
        var result = new VectorTile();
        result.Layers.Capacity = tile.Layers.Count;
        foreach (var layer in tile.Layers)
        {
            var decoded = new VectorTileLayer { Name = layer.Name ?? "[Unnamed]" };
            decoded.Features.Capacity = layer.Features.Count;
            foreach (var feature in layer.Features)
            {
                var tags = feature.Tags;
                var decodedFeature = new VectorTileFeature(tags.Count / 2, 0)
                {
                    Extent = 1,
                    GeometryType = PbfTileSource.ConvertGeometryType(feature.Type)
                };
                PbfGeometryDecoder.Decode(feature.Geometry, feature.Type, layer.Extent, decodedFeature.Geometry);
                for (var i = 0; i < tags.Count; i += 2)
                {
                    var key = layer.Keys[(int)tags[i]];
                    var value = layer.Values[(int)tags[i + 1]];
                    decodedFeature.Attributes[key] = GetAttributeValue(value);
                }
                decoded.Features.Add(decodedFeature);
            }
            result.Layers.Add(decoded);
        }
        return result;
    }

    public static List<Mapbox.Vector.Tile.VectorTileLayer> Parse(Stream stream)
    {
        var tile = Serializer.Deserialize<Tile>(stream);
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
