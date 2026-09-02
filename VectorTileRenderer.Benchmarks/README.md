# VectorTileRenderer benchmarks

This project is intentionally separate from the test suite. It contains current
implementations alongside local legacy baselines so alternative implementations
can be measured before they are moved into the reusable library.

Run all benchmarks:

```powershell
dotnet run --project VectorTileRenderer.Benchmarks -c Release
```

Run one benchmark group with a shorter exploratory job:

```powershell
dotnet run --project VectorTileRenderer.Benchmarks -c Release -- --filter "*QuadTree*" --job short
```

BenchmarkDotNet writes reports under `BenchmarkDotNet.Artifacts/`, which is
ignored by Git.
