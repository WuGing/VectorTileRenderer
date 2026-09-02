using BenchmarkDotNet.Attributes;

namespace VectorTileRenderer.Benchmarks;

[MemoryDiagnoser]
public class QuadTreeBenchmarks
{
    [Params(8, 14, 22)]
    public int Zoom { get; set; }

    [Benchmark(Baseline = true)]
    public string LegacyStringConcatenation()
    {
        var tx = (1 << Zoom) / 3;
        var ty = (1 << Zoom) / 2;
        var result = "";
        ty = (1 << Zoom) - 1 - ty;

        for (var i = Zoom; i >= 1; i--)
        {
            var digit = 0;
            var mask = 1 << (i - 1);

            if ((tx & mask) != 0)
                digit += 1;

            if ((ty & mask) != 0)
                digit += 2;

            result += digit;
        }

        return result;
    }

    [Benchmark]
    public string PreallocatedCharacterBuffer()
    {
        var tx = (1 << Zoom) / 3;
        var ty = (1 << Zoom) / 2;
        return GlobalMercator.QuadTree(tx, ty, Zoom);
    }
}
