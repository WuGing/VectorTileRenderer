using BenchmarkDotNet.Attributes;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Benchmarks;

[MemoryDiagnoser]
public class TilePathBenchmarks
{
    private const string Template = "cache/{z}/region-{x}/tile-{x}-{y}.pbf";

    [Benchmark(Baseline = true)]
    public string RasterChainedReplace()
    {
        return Template
            .Replace("{x}", "1439")
            .Replace("{y}", "1227")
            .Replace("{z}", "13");
    }

    [Benchmark]
    public string FlexibleSinglePassResolver() => TilePathResolver.Resolve(Template, 1439, 1227, 13);
}
