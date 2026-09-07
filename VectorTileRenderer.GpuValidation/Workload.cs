using WuGing.VectorTileRenderer.Sources;

namespace WuGing.VectorTileRenderer.GpuValidation;

internal sealed record Workload(string Name, Style Style, IVectorTileSource Source, int X = 0, int Y = 0, int Zoom = 14)
{
    public static Workload Synthetic(string output, string fonts, bool textHeavy)
    {
        if (!File.Exists(Path.Combine(fonts, "OpenSans Regular.ttf")))
        {
            throw new FileNotFoundException("The synthetic fixture requires the checked-in OpenSans Regular.ttf font.");
        }
        var path = Path.Combine(output, textHeavy ? "text-style.json" : "geometry-style.json");
        File.WriteAllText(path, """
            {
              "version": 8,
              "sources": { "tiles": { "type": "vector" } },
              "layers": [
                { "id": "background", "type": "background", "paint": { "background-color": "#e8eef2" } },
                { "id": "areas", "type": "fill", "source": "tiles", "source-layer": "areas", "paint": { "fill-color": "#72ab82", "fill-opacity": 0.8 } },
                { "id": "roads", "type": "line", "source": "tiles", "source-layer": "roads", "paint": { "line-color": "#a84f36", "line-width": 2 }, "layout": { "line-cap": "round" } },
                { "id": "labels", "type": "symbol", "source": "tiles", "source-layer": "labels",
                  "layout": { "text-field": "{name}", "text-font": ["OpenSans Regular"], "text-size": 14, "text-max-width": 20 },
                  "paint": { "text-color": "#172332", "text-halo-color": "#ffffff", "text-halo-width": 1 } }
              ]
            }
            """);
        var tile = new VectorTile();
        var areas = new VectorTileLayer { Name = "areas" };
        var roads = new VectorTileLayer { Name = "roads" };
        var labels = new VectorTileLayer { Name = "labels" };
        tile.Layers.AddRange([areas, roads, labels]);
        var grid = textHeavy ? 3 : 12;
        for (var y = 0; y < grid; y++)
        {
            for (var x = 0; x < grid; x++)
            {
                var left = 0.05 + x * 0.9 / grid;
                var top = 0.05 + y * 0.9 / grid;
                var size = 0.6 / grid;
                areas.Features.Add(new VectorTileFeature
                {
                    Extent = 1, GeometryType = "Polygon",
                    Geometry = [[new(left, top), new(left + size, top), new(left + size, top + size),
                        new(left, top + size), new(left, top)]]
                });
            }
        }
        for (var i = 0; i < (textHeavy ? 3 : 40); i++)
        {
            var y = 0.08 + i * 0.84 / (textHeavy ? 3 : 40);
            roads.Features.Add(new VectorTileFeature
            {
                Extent = 1, GeometryType = "LineString",
                Geometry = [[new(0.04, y), new(0.3, y + 0.025), new(0.65, y - 0.025), new(0.96, y)]]
            });
        }
        for (var y = 0; y < (textHeavy ? 10 : 2); y++)
        {
            for (var x = 0; x < (textHeavy ? 6 : 2); x++)
            {
                labels.Features.Add(new VectorTileFeature
                {
                    Extent = 1, GeometryType = "Point", Attributes = { ["name"] = $"Map {x}-{y}" },
                    Geometry = [[new(0.12 + x * 0.76 / (textHeavy ? 5 : 1), 0.1 + y * 0.8 / (textHeavy ? 9 : 1))]]
                });
            }
        }
        var source = new MemorySource(tile);
        var style = new Style(path) { FontDirectory = fonts };
        style.SetSourceProvider("tiles", source);
        return new Workload(textHeavy ? "synthetic-text" : "synthetic-geometry", style, source);
    }

    public static Workload Zurich(string root, bool decoded)
    {
        var pbf = new PbfTileSource(Path.Combine(root, "tiles", "zurich.pbf.gz"));
        IVectorTileSource source = decoded ? new MemorySource(pbf.GetVectorTile(0, 0, 14).GetAwaiter().GetResult()) : pbf;
        var style = new Style(Path.Combine(root, "styles", "basic-style.json"))
        {
            FontDirectory = Path.Combine(root, "styles", "fonts")
        };
        style.SetSourceProvider(0, source);
        return new Workload(decoded ? "zurich-decoded" : "zurich-file-decode", style, source);
    }

    internal sealed class MemorySource(VectorTile tile) : IVectorTileSource
    {
        public Task<Stream> GetTile(int x, int y, int z) => throw new NotSupportedException();
        public Task<VectorTile> GetVectorTile(int x, int y, int z) => Task.FromResult(tile);
    }

    internal sealed class YieldingSource(IVectorTileSource source) : IVectorTileSource
    {
        public Task<Stream> GetTile(int x, int y, int z) => throw new NotSupportedException();
        public async Task<VectorTile> GetVectorTile(int x, int y, int z)
        {
            await Task.Delay(20).ConfigureAwait(false);
            return await source.GetVectorTile(x, y, z).ConfigureAwait(false);
        }
    }
}
