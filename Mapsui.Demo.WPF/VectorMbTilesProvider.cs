using BruTile;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VectorTileRenderer;

namespace Mapsui.Demo.WPF
{
    class VectorMbTilesProvider
    {
        Style style;
        VectorTileRenderer.Sources.MbTilesSource provider;
        string cachePath;

        private static readonly object profileLock = new object();
        private static bool profileHooked;
        private static int profileSampleCount;
        private static double totalBuildMs;
        private static double totalFetchDecodeMs;
        private static double totalStyleEvalMs;
        private static double totalGeometryMs;
        private static double totalTextMs;
        private static double totalRenderMs;
        private static int totalFeatureCandidates;
        private static int totalFeatureAccepted;
        private static int totalGpuRenderedTiles;
        private static int totalCpuRenderedTiles;
        private static int totalUnknownRenderedTiles;
        private static readonly ConcurrentDictionary<(int X, int Y, int Z), byte> prefetchedTiles = new ConcurrentDictionary<(int X, int Y, int Z), byte>();
        private static readonly SemaphoreSlim prefetchLimiter = new SemaphoreSlim(2);

        private const int ProfileLogEveryNSamples = 40;
        private static readonly bool EnableNeighborPrefetch = false;
        private const RenderBackend SelectedBackend = RenderBackend.Auto;
        private const int PrefetchRadius = 1;
        private const int MaxPrefetchedTileKeys = 50000;

        public static event Action<string> ProfileSummaryUpdated;

        public VectorMbTilesProvider(string path, string stylePath, string cachePath)
        {
            this.cachePath = cachePath;

            HookRendererProfiling();

            style = new Style(stylePath)
            {
                FontDirectory = @"styles/fonts/"
            };

            provider = new VectorTileRenderer.Sources.MbTilesSource(path);
            style.SetSourceProvider("openmaptiles", provider);
        }

        private static void HookRendererProfiling()
        {
            lock (profileLock)
            {
                if (profileHooked)
                {
                    return;
                }

                Renderer.ProfileSink = OnRenderProfile;
                profileHooked = true;
            }
        }

        private static void OnRenderProfile(Renderer.RenderProfile profile)
        {
            lock (profileLock)
            {
                profileSampleCount++;
                totalBuildMs += profile.BuildVisualLayersMs;
                totalFetchDecodeMs += profile.TileFetchDecodeMs;
                totalStyleEvalMs += profile.BuildStyleEvalMs;
                totalGeometryMs += profile.DrawGeometryMs;
                totalTextMs += profile.DrawTextMs;
                totalRenderMs += profile.TotalMs;
                totalFeatureCandidates += profile.FeatureCandidateCount;
                totalFeatureAccepted += profile.FeatureAcceptedCount;

                if (string.Equals(profile.Backend, "GPU", StringComparison.OrdinalIgnoreCase))
                {
                    totalGpuRenderedTiles++;
                }
                else if (string.Equals(profile.Backend, "CPU", StringComparison.OrdinalIgnoreCase))
                {
                    totalCpuRenderedTiles++;
                }
                else
                {
                    totalUnknownRenderedTiles++;
                }

                if (profileSampleCount < ProfileLogEveryNSamples)
                {
                    return;
                }

                var avgBuild = totalBuildMs / profileSampleCount;
                var avgFetchDecode = totalFetchDecodeMs / profileSampleCount;
                var avgStyleEval = totalStyleEvalMs / profileSampleCount;
                var avgGeometry = totalGeometryMs / profileSampleCount;
                var avgText = totalTextMs / profileSampleCount;
                var avgTotal = totalRenderMs / profileSampleCount;
                var avgFeatureCandidates = totalFeatureCandidates / (double)profileSampleCount;
                var avgFeatureAccepted = totalFeatureAccepted / (double)profileSampleCount;

                Trace.WriteLine(
                    string.Format(
                        "[VectorTileRenderer][Mapsui] avg over {0} tiles => total: {1:0.00} ms, build: {2:0.00} ms (fetch: {3:0.00}, style: {4:0.00}), geometry: {5:0.00} ms, text: {6:0.00} ms, feat: {7:0.0}/{8:0.0}, backend gpu/cpu/u: {9}/{10}/{11}",
                        profileSampleCount,
                        avgTotal,
                        avgBuild,
                        avgFetchDecode,
                        avgStyleEval,
                        avgGeometry,
                        avgText,
                        avgFeatureAccepted,
                        avgFeatureCandidates,
                        totalGpuRenderedTiles,
                        totalCpuRenderedTiles,
                        totalUnknownRenderedTiles));

                var summary = string.Format(
                    "Tiles avg ({0}): total {1:0.00} ms | build {2:0.00} (fetch {3:0.00}, style {4:0.00}) | geom {5:0.00} | text {6:0.00} | feat {7:0}/{8:0} | backend G/C/U {9}/{10}/{11}",
                    profileSampleCount,
                    avgTotal,
                    avgBuild,
                    avgFetchDecode,
                    avgStyleEval,
                    avgGeometry,
                    avgText,
                    avgFeatureAccepted,
                    avgFeatureCandidates,
                    totalGpuRenderedTiles,
                    totalCpuRenderedTiles,
                    totalUnknownRenderedTiles);

                var summaryUpdated = ProfileSummaryUpdated;
                if (summaryUpdated != null)
                {
                    summaryUpdated(summary);
                }

                profileSampleCount = 0;
                totalBuildMs = 0;
                totalFetchDecodeMs = 0;
                totalStyleEvalMs = 0;
                totalGeometryMs = 0;
                totalTextMs = 0;
                totalRenderMs = 0;
                totalFeatureCandidates = 0;
                totalFeatureAccepted = 0;
                totalGpuRenderedTiles = 0;
                totalCpuRenderedTiles = 0;
                totalUnknownRenderedTiles = 0;
            }
        }

        public async Task<byte[]> GetTileAsync(TileInfo tileInfo)
        {
            var canvas = CanvasFactory.Create(SelectedBackend);
            var backendName = GetBackendName(canvas);
            SKBitmap bitmap;
            var x = (int)tileInfo.Index.Col;
            var y = (int)tileInfo.Index.Row;
            var z = Convert.ToInt32(tileInfo.Index.Level);

            try
            {
                var previousBackendHint = Renderer.CurrentBackendHint;
                Renderer.CurrentBackendHint = backendName;
                try
                {
                    bitmap = await Renderer.RenderCached(
                        cachePath,
                        style,
                        canvas,
                        x,
                        y,
                        z,
                        256,
                        256,
                        1);
                }
                finally
                {
                    Renderer.CurrentBackendHint = previousBackendHint;
                }
            }
            catch
            {
                return null;
            }

            if (EnableNeighborPrefetch)
            {
                TriggerNeighborPrefetch(x, y, z);
            }

            return GetBytesFromBitmap(bitmap);
        }

        private void TriggerNeighborPrefetch(int x, int y, int z)
        {
            for (int dy = -PrefetchRadius; dy <= PrefetchRadius; dy++)
            {
                for (int dx = -PrefetchRadius; dx <= PrefetchRadius; dx++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    QueuePrefetchTile(x + dx, y + dy, z);
                }
            }
        }

        private void QueuePrefetchTile(int x, int y, int z)
        {
            var key = (x, y, z);
            if (!prefetchedTiles.TryAdd(key, 0))
            {
                return;
            }

            if (prefetchedTiles.Count > MaxPrefetchedTileKeys)
            {
                prefetchedTiles.Clear();
                prefetchedTiles.TryAdd(key, 0);
            }

            _ = Task.Run(async () =>
            {
                await prefetchLimiter.WaitAsync();
                try
                {
                    var prefetchCanvas = CanvasFactory.Create(SelectedBackend);
                    var backendName = GetBackendName(prefetchCanvas);
                    var previousBackendHint = Renderer.CurrentBackendHint;
                    Renderer.CurrentBackendHint = backendName;
                    try
                    {
                        await Renderer.RenderCached(cachePath, style, prefetchCanvas, x, y, z, 256, 256, 1);
                    }
                    finally
                    {
                        Renderer.CurrentBackendHint = previousBackendHint;
                    }
                }
                catch
                {
                    // Best effort prefetch only.
                }
                finally
                {
                    prefetchLimiter.Release();
                }
            });
        }

        private static string GetBackendName(ICanvas canvas)
        {
            if (canvas is SkiaGpuCanvas gpuCanvas && gpuCanvas.IsGpuEnabled)
            {
                return "GPU";
            }

            return "CPU";
        }

        static byte[] GetBytesFromBitmap(SKBitmap bmp)
        {
            if (bmp == null)
            {
                return null;
            }

            using (var image = SKImage.FromBitmap(bmp))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                return data.ToArray();
            }
        }
    }
}
