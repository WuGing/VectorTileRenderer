namespace WuGing.VectorTileRenderer;

public class VectorTile
{
    public bool IsOverZoomed { get; set; } = false;
    public List<VectorTileLayer> Layers = [];

    public VectorTile ApplyExtent(Rect extent)
    {
        VectorTile newTile = new()
        {
            IsOverZoomed = IsOverZoomed,
            Layers = new List<VectorTileLayer>(Layers.Count)
        };

        foreach (var layer in Layers)
        {
            var vectorLayer = new VectorTileLayer
            {
                Name = layer.Name,
                Features = new List<VectorTileFeature>(layer.Features.Count)
            };

            foreach (var feature in layer.Features)
            {
                var vectorFeature = new VectorTileFeature
                {
                    Attributes = new Dictionary<string, object>(feature.Attributes),
                    Extent = feature.Extent,
                    GeometryType = feature.GeometryType
                };

                var vectorGeometry = new List<List<Point>>(feature.Geometry.Count);
                var xRange = extent.Right - extent.Left;
                var yRange = extent.Bottom - extent.Top;
                foreach (var geometry in feature.Geometry)
                {
                    var vectorPoints = new List<Point>(geometry.Count);

                    foreach (var point in geometry)
                    {
                        var newX = xRange == 0 ? 0 : (point.X - extent.Left) * vectorFeature.Extent / xRange;
                        var newY = yRange == 0 ? 0 : (point.Y - extent.Top) * vectorFeature.Extent / yRange;

                        vectorPoints.Add(new Point(newX, newY));
                    }

                    vectorGeometry.Add(vectorPoints);
                }

                vectorFeature.Geometry = vectorGeometry;
                vectorLayer.Features.Add(vectorFeature);
            }

            newTile.Layers.Add(vectorLayer);
        }

        return newTile;
    }
}

public class VectorTileLayer
{
    public string Name { get; set; }

    public List<VectorTileFeature> Features = [];
}

public class VectorTileFeature
{
    public double Extent { get; set; }
    public string GeometryType { get; set; }
    public Dictionary<string, object> Attributes = [];
    public List<List<Point>> Geometry = [];
}
