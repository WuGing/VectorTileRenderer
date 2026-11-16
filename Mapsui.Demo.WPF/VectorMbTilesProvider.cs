using BruTile;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WuGing.VectorTileRenderer;

namespace Mapsui.Demo.WPF
{
    class VectorMbTilesProvider : IDisposable
    {
        private readonly Style style;
        private readonly string cachePath;
        private readonly string sourceRevision;
        private readonly WuGing.VectorTileRenderer.Sources.SingleMbTilesSource provider;
        private readonly object lifetimeLock = new();
        private int activeRequests;
        private bool retired;
        private bool released;
        private static readonly object profileLock = new();
        private static readonly Queue<double> requestTimes = new();
        private static int hits;
        private static int misses;
        private static string lastRender = "No fresh renders yet";
        public static event Action<string> ProfileSummaryUpdated;

        public VectorMbTilesProvider(string path, string stylePath, string cachePath, int revision = 0)
        {
            this.cachePath = cachePath;
            var file = new FileInfo(path);
            // Demo policy: metadata plus a persistent manual revision, not a content digest.
            sourceRevision = $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}|{revision}";
            style = new Style(stylePath)
            {
                FontDirectory = Path.Combine(AppContext.BaseDirectory, "styles", "fonts")
            };
            provider = new WuGing.VectorTileRenderer.Sources.SingleMbTilesSource(path);
            style.SetSourceProvider("openmaptiles", provider);
        }

        public static void StartProfiling()
        {
            Renderer.ProfileSink = OnRenderProfile;
            Renderer.CacheProfileSink = OnCacheProfile;
        }

        public static void StopProfiling()
        {
            Renderer.ProfileSink = null;
            Renderer.CacheProfileSink = null;
        }

        private static void OnRenderProfile(Renderer.RenderProfile profile)
        {
            lock (profileLock)
            {
                lastRender = $"Last render: {profile.ActualBackend}, geometry {profile.DrawGeometryMs:0.0} ms, text {profile.DrawTextOnlyMs:0.0}, finish {profile.FinishDrawingMs:0.0}";
            }
        }

        private static void OnCacheProfile(CacheRequestProfile profile)
        {
            string summary;
            lock (profileLock)
            {
                if (profile.CacheHit) hits++;
                else misses++;
                requestTimes.Enqueue(profile.TotalMs);
                if (requestTimes.Count > 200) requestTimes.Dequeue();
                var sorted = requestTimes.OrderBy(value => value).ToArray();
                var median = sorted[(sorted.Length - 1) / 2];
                var p95 = sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1];
                summary = $"Session disk hits/misses: {hits}/{misses} | last {requestTimes.Count} requests median/p95: {median:0.0}/{p95:0.0} ms\n" +
                    $"Last request: {profile.ActualBackend}, total {profile.TotalMs:0.0} ms | lookup {profile.LookupMs:0.0}, render {profile.RenderMs:0.0}, encode/write {profile.EncodeAndPublishMs:0.0}\n" +
                    lastRender;
            }
            Trace.WriteLine(summary);
            ProfileSummaryUpdated?.Invoke(summary);
        }

        public async Task<byte[]> GetTileAsync(TileInfo tileInfo)
        {
            lock (lifetimeLock)
            {
                if (retired) return null;
                activeRequests++;
            }
            try
            {
                var canvas = CanvasFactory.Create(RenderBackend.Auto);
                using var lifetime = canvas as IDisposable;
                return await Renderer.RenderCachedPng(cachePath, style, canvas,
                    (int)tileInfo.Index.Col, (int)tileInfo.Index.Row, Convert.ToInt32(tileInfo.Index.Level),
                    sourceRevision, 256, 256, 1);
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"Tile request failed: {exception}");
                ProfileSummaryUpdated?.Invoke($"Tile request failed: {exception.Message}");
                return null;
            }
            finally
            {
                lock (lifetimeLock)
                {
                    activeRequests--;
                    ReleaseRetiredProvider();
                }
            }
        }

        public void Dispose()
        {
            lock (lifetimeLock)
            {
                retired = true;
                ReleaseRetiredProvider();
            }
        }

        private void ReleaseRetiredProvider()
        {
            if (retired && activeRequests == 0 && !released)
            {
                released = true;
                provider.Dispose();
            }
        }
    }
}
