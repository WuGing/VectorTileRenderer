using Mapbox.Vector.Tile;

namespace WuGing.VectorTileRenderer.Sources;

// Adapted from mapbox-vector-tile 5.3.0 GeometryParser.cs. See THIRD-PARTY-NOTICES.txt.
internal static class PbfGeometryDecoder
{
    public static void Decode(IReadOnlyList<uint> geometry, Tile.GeomType type, uint extent, List<List<Point>> result)
    {
        long x = 0;
        long y = 0;
        var inserted = 0;
        var segmentStart = 0;
        var index = 0;
        uint remaining = 0;
        uint command = 0;
        List<Point> points = null;

        while (index < geometry.Count)
        {
            if (remaining == 0)
            {
                remaining = geometry[index++];
                command = remaining & 7;
                remaining >>= 3;
            }

            if (remaining > 0 && command == 1 && inserted > segmentStart)
            {
                result.Add(points);
                points = null;
                segmentStart = inserted;
            }

            if (command == 7)
            {
                if (remaining == 0 && index < geometry.Count
                    && (type == Tile.GeomType.Point || inserted == segmentStart))
                {
                    throw new InvalidDataException("A zero-count ClosePath cannot advance geometry decoding.");
                }
                if (type != Tile.GeomType.Point && inserted > segmentStart)
                {
                    // The dependency bounds its coordinate storage by the wire-word count.
                    if (inserted >= geometry.Count) throw new IndexOutOfRangeException();
                    points.Add(points[0]);
                    inserted++;
                }
                remaining--;
                continue;
            }

            var dx = geometry[index++];
            var dy = geometry[index++];
            remaining--;
            x += ZigZag.Decode(dx);
            y += ZigZag.Decode(dy);
            if (inserted >= geometry.Count) throw new IndexOutOfRangeException();
            points ??= new List<Point>();
            points.Add(new Point(x / (double)extent, y / (double)extent));
            inserted++;
        }

        if (inserted > segmentStart) result.Add(points);
    }
}
