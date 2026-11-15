using NUnit.Framework;
using SkiaSharp;
using WuGing.VectorTileRenderer.Sources;

namespace VectorTileRenderer.Tests;

[TestFixture]
[NonParallelizable]
public class RendererTests
{
    [TearDown]
    public void TearDown()
    {
        Renderer.ProfileSink = null;
        Renderer.CacheProfileSink = null;
        Renderer.CurrentBackendHint = null;
    }

    [Test]
    public async Task Render_DrawsMatchingGeometryAndReportsProfile()
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var canvas = new RecordingCanvas();
        Renderer.RenderProfile profile = null;
        Renderer.CurrentBackendHint = "Test";
        Renderer.ProfileSink = value => profile = value;

        using var bitmap = await Renderer.Render(style, canvas, 1, 2, 8, 256, 256);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.LineStrings, Has.Count.EqualTo(1));
            Assert.That(canvas.LineStrings[0], Is.EqualTo(new[] { new Point(0, 0), new Point(256, 256) }));
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.Backend, Is.EqualTo("Test"));
            Assert.That(profile.GeometryDrawCallCount, Is.EqualTo(1));
            Assert.That(profile.FeatureAcceptedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Render_ReturnsNull_WhenVectorTileIsMissing()
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(null));
        var canvas = new RecordingCanvas();

        var bitmap = await Renderer.Render(style, canvas, 1, 2, 8, 256, 256);

        Assert.Multiple(() =>
        {
            Assert.That(bitmap, Is.Null);
            Assert.That(canvas.FinishCalled, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Render_PropagatesFinishFailure_WithoutPublishingSuccess(bool cached)
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var failure = new InvalidOperationException("Simulated pixel readback failure");
        var canvas = new RecordingCanvas { FinishFailure = failure };
        Renderer.RenderProfile profile = null;
        Renderer.ProfileSink = value => profile = value;
        var cachePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"failed-render-{Guid.NewGuid():N}");
        CacheRequestProfile cacheProfile = null;
        Renderer.CacheProfileSink = value => cacheProfile = value;

        try
        {
            await Assert.ThatAsync(async () =>
            {
                using var bitmap = cached
                    ? await Renderer.RenderCached(cachePath, style, canvas, 1, 2, 8, 256, 256)
                    : await Renderer.Render(style, canvas, 1, 2, 8, 256, 256);
            }, Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo(failure.Message));
            Assert.That(canvas.FinishCalled, Is.True);
            Assert.That(profile, Is.Null);
            Assert.That(cacheProfile, Is.Null);
            if (cached)
            {
                Assert.That(Directory.GetFiles(cachePath), Is.Empty);
            }
        }
        finally
        {
            // Remove only this test's empty directory; never recursively remove output.
            if (Directory.Exists(cachePath) && !Directory.EnumerateFileSystemEntries(cachePath).Any())
            {
                Directory.Delete(cachePath);
            }
        }
    }

    [TestCase(0)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public async Task RenderCached_IgnoresTilesFromBeforeLabelPlacementFix(int renderVersion)
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var cachePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"legacy-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cachePath);
        object legacyBundle = renderVersion > 0
            ? new { RenderVersion = renderVersion, style.Hash, sizeX = 256d, sizeY = 256d, scale = 1d, layerString = "" }
            : new { style.Hash, sizeX = 256d, sizeY = 256d, scale = 1d, layerString = "" };
        var legacyKey = System.Text.Json.JsonSerializer.Serialize(legacyBundle);
        var legacyPath = Path.Combine(cachePath, "1x2-8-" + Utils.Sha256(legacyKey).Substring(0, 12) + ".png");
        using (var oldBitmap = new SKBitmap(1, 1))
        using (var data = oldBitmap.Encode(SKEncodedImageFormat.Png, 100))
        {
            File.WriteAllBytes(legacyPath, data.ToArray());
        }
        // A fresh render throws; loading the legacy PNG would incorrectly succeed.
        var canvas = new RecordingCanvas { FinishFailure = new InvalidOperationException("Fresh render") };
        try
        {
            await Assert.ThatAsync(() => Renderer.RenderCached(cachePath, style, canvas, 1, 2, 8, 256, 256),
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Fresh render"));
            Assert.That(canvas.FinishCalled, Is.True);
        }
        finally
        {
            File.Delete(legacyPath);
            Directory.Delete(cachePath);
        }
    }

    [Test]
    public async Task RenderCached_CompletesPublicationBeforeReturningCallerOwnedBitmap()
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "cache-ownership-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var canvas = new SkiaCanvas())
            using (var image = await Renderer.RenderCached(directory, style, canvas, 1, 2, 8, 256, 256))
            {
                Assert.That(Directory.GetFiles(directory, "*.png"), Has.Length.EqualTo(1));
            }
            var shouldNotRender = new RecordingCanvas { FinishFailure = new InvalidOperationException("Unexpected cache miss") };
            using var cached = await Renderer.RenderCached(directory, style, shouldNotRender, 1, 2, 8, 256, 256);
            Assert.That(cached.Width, Is.EqualTo(256));
            Assert.That(shouldNotRender.FinishCalled, Is.False);
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Render_KeepsSharedRasterAliveUntilRequestEnds(bool failFinish)
    {
        var path = TestAssets.WriteTemporaryStyle("""
            { "sources": { "tiles": { "type": "raster" } }, "layers": [
              { "id": "one", "type": "raster", "source": "tiles" },
              { "id": "two", "type": "raster", "source": "tiles" }
            ] }
            """);
        var style = new Style(path);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        style.SetSourceProvider("tiles", new RasterSource(stream));
        var canvas = new RecordingCanvas { FinishFailure = failFinish ? new InvalidOperationException("Finish failed") : null };
        if (failFinish)
        {
            await Assert.ThatAsync(() => Renderer.Render(style, canvas, 1, 2, 8, 256, 256),
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Finish failed"));
        }
        else
        {
            using var image = await Renderer.Render(style, canvas, 1, 2, 8, 256, 256);
        }
        Assert.That(canvas.ReadableRasters, Is.EqualTo(new[] { true, true }));
        Assert.That(stream.CanRead, Is.False, "Renderer must release provider streams on success and failure.");
    }

    private sealed class RasterSource(Stream stream) : ITileSource
    {
        public Task<Stream> GetTile(int x, int y, int z) => Task.FromResult(stream);
    }

    [TestCase(null)]
    [TestCase("dataset:v1")]
    public async Task RenderCached_ConcurrentMissesPublishOneCompleteFile(string sourceRevision)
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "cache-concurrent-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
            {
                using var canvas = new SkiaCanvas();
                using var image = await Renderer.RenderCached(directory, style, canvas, 1, 2, 8, sourceRevision, 256, 256);
                Assert.That(image.Width, Is.EqualTo(256));
            })));
            var files = Directory.GetFiles(directory);
            Assert.That(files, Has.Length.EqualTo(1));
            using var decoded = SKBitmap.Decode(files[0]);
            Assert.That(decoded.Width, Is.EqualTo(256));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
    }

    [TestCase(false, 1, null)]
    [TestCase(true, 1, null)]
    [TestCase(false, 8, null)]
    [TestCase(false, 1, "dataset:v1")]
    [TestCase(true, 1, "dataset:v1")]
    [TestCase(false, 8, "dataset:v1")]
    public async Task RenderCached_ReplacesCorruptEntryAndReusesRepairedTile(bool empty, int requests, string sourceRevision)
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "cache-corrupt-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var canvas = new SkiaCanvas())
            using (var image = await Renderer.RenderCached(directory, style, canvas, 1, 2, 8, sourceRevision, 256, 256))
            {
                Assert.That(image, Is.Not.Null);
            }
            var path = Directory.GetFiles(directory, "*.png").Single();
            File.WriteAllBytes(path, empty ? Array.Empty<byte>() : new byte[] { 1, 2, 3, 4 });
            await Task.WhenAll(Enumerable.Range(0, requests).Select(_ => Task.Run(async () =>
            {
                using var canvas = new SkiaCanvas();
                using var image = await Renderer.RenderCached(directory, style, canvas, 1, 2, 8, sourceRevision, 256, 256);
                Assert.That(image, Is.Not.Null);
                Assert.That(image.Width, Is.EqualTo(256));
            })));
            using var repaired = SKBitmap.Decode(path);
            Assert.That(repaired, Is.Not.Null);
            Assert.That(repaired.Width, Is.EqualTo(256));
            var shouldNotRender = new RecordingCanvas { FinishFailure = new InvalidOperationException("Unexpected cache miss") };
            using var cached = await Renderer.RenderCached(directory, style, shouldNotRender, 1, 2, 8, sourceRevision, 256, 256);
            Assert.That(cached.Pixels, Is.EqualTo(repaired.Pixels));
            Assert.That(shouldNotRender.FinishCalled, Is.False);
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
    }

    [TestCase("dataset-a:v1", "dataset-b:v1")]
    [TestCase("dataset-a:v1", "dataset-a:v2")]
    [TestCase(null, "")]
    public async Task RenderCached_IsolatesSourceRevisionsAndReusesMatchingRevision(string firstRevision, string secondRevision)
    {
        var style = CreateLineStyle();
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "cache-revision-" + Guid.NewGuid().ToString("N"));
        try
        {
            style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
            using var firstCanvas = new SkiaCanvas();
            using var first = await Renderer.RenderCached(directory, style, firstCanvas, 1, 2, 8, firstRevision, 256, 256);

            var changedTile = CreateLineTile();
            changedTile.Layers[0].Features[0].Geometry = [[new Point(0, 0.5), new Point(1, 0.5)]];
            style.SetSourceProvider("tiles", new StubVectorTileSource(changedTile));
            using var secondCanvas = new SkiaCanvas();
            using var second = await Renderer.RenderCached(directory, style, secondCanvas, 1, 2, 8, secondRevision, 256, 256);
            Assert.That(second.Pixels.SequenceEqual(first.Pixels), Is.False, "Changed source data must not reuse the first provider's PNG.");
            Assert.That(Directory.GetFiles(directory, "*.png"), Has.Length.EqualTo(2));

            // A new Style/provider instance must still hit the persistent revision key.
            var freshStyle = CreateLineStyle();
            Assert.That(freshStyle.Hash, Is.EqualTo(style.Hash));
            freshStyle.SetSourceProvider("tiles", new StubVectorTileSource(null));
            var shouldNotRender = new RecordingCanvas { FinishFailure = new InvalidOperationException("Unexpected cache miss") };
            using var cached = await Renderer.RenderCached(directory, freshStyle, shouldNotRender, 1, 2, 8, secondRevision, 256, 256);
            Assert.That(cached, Is.Not.Null);
            Assert.That(cached.Pixels.SequenceEqual(second.Pixels), Is.True);
            Assert.That(shouldNotRender.FinishCalled, Is.False);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
    }

    [Test]
    public async Task RenderCached_PreservesExistingUnversionedCacheKeyAndSignature()
    {
        var style = CreateLineStyle();
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "cache-compatible-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var bundle = new { RenderVersion = 5, style.Hash, sizeX = 256d, sizeY = 256d, scale = 1d, layerString = "" };
        var key = Utils.Sha256(System.Text.Json.JsonSerializer.Serialize(bundle)).Substring(0, 12);
        var path = Path.Combine(directory, "1x2-8-" + key + ".png");
        try
        {
            using (var image = new SKBitmap(256, 256))
            {
                image.Erase(SKColors.Red);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(path, data.ToArray());
            }
            // Binding this exact delegate also checks that the original public signature remains.
            Func<string, Style, ICanvas, int, int, double, double, double, double, List<string>, Task<SKBitmap>> legacy = Renderer.RenderCached;
            var canvas = new RecordingCanvas { FinishFailure = new InvalidOperationException("Unexpected cache miss") };
            using var oldCall = await legacy(directory, style, canvas, 1, 2, 8, 256, 256, 1, null);
            using var nullRevision = await Renderer.RenderCached(directory, style, canvas, 1, 2, 8, sourceRevision: null, sizeX: 256, sizeY: 256);
            Assert.That(oldCall.GetPixel(0, 0), Is.EqualTo(SKColors.Red));
            Assert.That(nullRevision.GetPixel(0, 0), Is.EqualTo(SKColors.Red));
            Assert.That(canvas.FinishCalled, Is.False);
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    [Test]
    public async Task RenderCached_RemainsUsableDuringConcurrentCacheMaintenance()
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "cache-maintenance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 16).Select(i => Task.Run(async () =>
            {
                if (i % 2 == 0) TileCache.Prune(directory, 0);
                using var canvas = new SkiaCanvas();
                using var image = await Renderer.RenderCached(directory, style, canvas, 1, 2, 8, "dataset:v1", 256, 256);
                TileCache.Prune(directory, 0);
                Assert.That(image.Width, Is.EqualTo(256));
                Assert.That(image.Pixels.Any(p => p.Alpha > 0), Is.True);
            })));
            using var finalCanvas = new SkiaCanvas();
            using var final = await Renderer.RenderCached(directory, style, finalCanvas, 1, 2, 8, "dataset:v1", 256, 256);
            var files = Directory.GetFiles(directory);
            Assert.That(files, Has.Length.EqualTo(1));
            using var decoded = SKBitmap.Decode(files[0]);
            Assert.That(decoded.Pixels.SequenceEqual(final.Pixels), Is.True);
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    [Test]
    public async Task RenderProfile_SeparatesFinishAndReportsCpuFallbackDespiteGpuHint()
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        Renderer.RenderProfile profile = null;
        Renderer.ProfileSink = value => profile = value;
        Renderer.CurrentBackendHint = "GPU";
        using var canvas = new SkiaGpuCanvas(null);
        using var image = await Renderer.Render(style, canvas, 1, 2, 8, 256, 256);
        Assert.That(profile.Backend, Is.EqualTo("GPU"), "Legacy hint semantics are preserved.");
        Assert.That(profile.ActualBackend, Is.EqualTo("CPU"));
        Assert.That(profile.FinishDrawingMs, Is.GreaterThan(0));
        Assert.That(profile.DrawTextOnlyMs, Is.GreaterThanOrEqualTo(0));
        Assert.That(profile.DrawTextMs, Is.EqualTo(profile.DrawTextOnlyMs + profile.FinishDrawingMs).Within(0.000001));
        Assert.That(profile.TotalMs, Is.GreaterThanOrEqualTo(profile.DrawTextMs));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CacheProfile_ReportsCompleteMissAndHitWithoutRenderingOnHit(bool throwingSink)
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "cache-profile-" + Guid.NewGuid().ToString("N"));
        var reports = new List<CacheRequestProfile>();
        var renders = 0;
        Renderer.ProfileSink = _ => renders++;
        Renderer.CacheProfileSink = value =>
        {
            // Publication must be complete when the observer runs.
            using var decoded = SKBitmap.Decode(Directory.GetFiles(directory, "*.png").Single());
            Assert.That(decoded, Is.Not.Null);
            reports.Add(value);
            if (throwingSink) throw new InvalidOperationException("Observer failure");
        };
        try
        {
            using var canvas = new SkiaCanvas();
            using var rendered = await Renderer.RenderCached(directory, style, canvas, 1, 2, 8, "dataset:v1", 256, 256);
            var shouldNotRender = new RecordingCanvas { FinishFailure = new InvalidOperationException("Unexpected render") };
            using var hit = await Renderer.RenderCached(directory, style, shouldNotRender, 1, 2, 8, "dataset:v1", 256, 256);
            Assert.That(reports, Has.Count.EqualTo(2));
            var miss = reports[0];
            Assert.That(miss.CacheHit, Is.False);
            Assert.That(miss.HasBitmap, Is.True);
            Assert.That(miss.ActualBackend, Is.EqualTo("CPU"));
            Assert.That(miss.RenderMs, Is.GreaterThan(0));
            Assert.That(miss.EncodeAndPublishMs, Is.GreaterThan(0));
            Assert.That(miss.TotalMs, Is.GreaterThanOrEqualTo(miss.LookupMs + miss.RenderMs + miss.EncodeAndPublishMs));
            var cached = reports[1];
            Assert.That(cached.CacheHit, Is.True);
            Assert.That(cached.HasBitmap, Is.True);
            Assert.That(cached.ActualBackend, Is.EqualTo("DiskCache"));
            Assert.That(cached.RenderMs, Is.Zero);
            Assert.That(cached.EncodeAndPublishMs, Is.Zero);
            Assert.That(cached.TotalMs, Is.GreaterThanOrEqualTo(cached.LookupMs));
            Assert.That(renders, Is.EqualTo(1));
            Assert.That(hit.Pixels.SequenceEqual(rendered.Pixels), Is.True);
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    [Test]
    public async Task CacheProfile_ReportsMissingTileWithoutClaimingSuccessfulRender()
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(null));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "cache-missing-profile-" + Guid.NewGuid().ToString("N"));
        CacheRequestProfile report = null;
        Renderer.CacheProfileSink = value => report = value;
        try
        {
            using var image = await Renderer.RenderCached(directory, style, new RecordingCanvas(), 1, 2, 8, 256, 256);
            Assert.That(image, Is.Null);
            Assert.That(report, Is.Not.Null);
            Assert.That(report.HasBitmap, Is.False);
            Assert.That(report.CacheHit, Is.False);
            Assert.That(report.ActualBackend, Is.EqualTo("Unknown"));
            Assert.That(report.EncodeAndPublishMs, Is.Zero);
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RenderCachedPng_SharesBitmapCacheKeysAndReturnsIndependentOriginalBytes(bool pngFirst)
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "png-shared-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var canvas = new SkiaCanvas();
            if (pngFirst)
                Assert.That(await Renderer.RenderCachedPng(directory, style, canvas, 1, 2, 8, "dataset:v1", 256, 256), Is.Not.Null);
            else
            {
                using var seed = await Renderer.RenderCached(directory, style, canvas, 1, 2, 8, "dataset:v1", 256, 256);
            }
            var file = Directory.GetFiles(directory).Single();
            var expected = File.ReadAllBytes(file);
            var shouldNotRender = new RecordingCanvas { FinishFailure = new InvalidOperationException("Unexpected miss") };
            CacheRequestProfile profile = null;
            Renderer.CacheProfileSink = value => { profile = value; throw new InvalidOperationException("Observer failure"); };
            var bytes = await Renderer.RenderCachedPng(directory, style, shouldNotRender, 1, 2, 8, "dataset:v1", 256, 256);
            Assert.That(bytes, Is.EqualTo(expected));
            Assert.That(profile.HasOutput, Is.True);
            Assert.That(profile.HasBitmap, Is.False);
            Assert.That(profile.OutputFormat, Is.EqualTo("Png"));
            Assert.That(profile.CacheHit, Is.True);
            Assert.That(profile.RenderMs, Is.Zero);
            Assert.That(profile.EncodeAndPublishMs, Is.Zero);
            using var bitmapHit = await Renderer.RenderCached(directory, style, shouldNotRender, 1, 2, 8, "dataset:v1", 256, 256);
            using var decoded = SKBitmap.Decode(bytes);
            Assert.That(decoded.Pixels, Is.EqualTo(bitmapHit.Pixels));
            bytes[0] = 0;
            Assert.That(File.ReadAllBytes(file), Is.EqualTo(expected));
            Assert.That(shouldNotRender.FinishCalled, Is.False);
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    [TestCase(0)]
    [TestCase(4)]
    [TestCase(21)]
    public async Task RenderCachedPng_RepairsEmptyInvalidAndTruncatedPng(int retainedBytes)
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "png-corrupt-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var canvas = new SkiaCanvas();
            var original = await Renderer.RenderCachedPng(directory, style, canvas, 1, 2, 8, 256, 256);
            var file = Directory.GetFiles(directory).Single();
            File.WriteAllBytes(file, original.Take(retainedBytes).ToArray());
            var repaired = await Renderer.RenderCachedPng(directory, style, canvas, 1, 2, 8, 256, 256);
            Assert.That(repaired, Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(file), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RenderCachedPng_ReturnsNullOrPropagatesFailureWithoutPublishing(bool failure)
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(failure ? CreateLineTile() : null));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "png-failure-" + Guid.NewGuid().ToString("N"));
        CacheRequestProfile report = null;
        Renderer.CacheProfileSink = value => report = value;
        try
        {
            var canvas = new RecordingCanvas { FinishFailure = new InvalidOperationException("Finish failed") };
            if (failure)
            {
                await Assert.ThatAsync(() => Renderer.RenderCachedPng(directory, style, canvas, 1, 2, 8),
                    Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Finish failed"));
                Assert.That(report, Is.Null);
            }
            else
            {
                Assert.That(await Renderer.RenderCachedPng(directory, style, canvas, 1, 2, 8), Is.Null);
                Assert.That(report.HasOutput, Is.False);
                Assert.That(report.HasBitmap, Is.False);
            }
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [Test]
    public async Task RenderCachedPng_DisposesInternalBitmapAndReturnsOutputWhenPublicationFails()
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "png-owned-" + Guid.NewGuid().ToString("N"));
        var canvas = new RecordingCanvas();
        try
        {
            var seed = await Renderer.RenderCachedPng(directory, style, canvas, 1, 2, 8);
            Assert.That(canvas.LastBitmap.Handle, Is.EqualTo(IntPtr.Zero));
            var file = Directory.GetFiles(directory).Single();
            File.Delete(file);
            // A directory at the PNG path prevents atomic publication without preventing rendering.
            Directory.CreateDirectory(file);
            var bytes = await Renderer.RenderCachedPng(directory, style, canvas, 1, 2, 8);
            Assert.That(bytes, Is.EqualTo(seed));
            Assert.That(canvas.LastBitmap.Handle, Is.EqualTo(IntPtr.Zero));
            Assert.That(Directory.GetFiles(directory), Is.Empty);
            Directory.Delete(file);
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            foreach (var child in Directory.GetDirectories(directory)) Directory.Delete(child);
            Directory.Delete(directory);
        }
    }

    [Test]
    public async Task RenderCachedPng_ConcurrentBitmapAndPngRequestsRemainUsableDuringPruning()
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "png-concurrent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Task.Run(async () =>
            {
                using var canvas = new SkiaCanvas();
                if (i % 2 == 0)
                {
                    var bytes = await Renderer.RenderCachedPng(directory, style, canvas, 1, 2, 8, "dataset:v1", 256, 256);
                    using var decoded = SKBitmap.Decode(bytes);
                    Assert.That(decoded.Width, Is.EqualTo(256));
                }
                else
                {
                    using var bitmap = await Renderer.RenderCached(directory, style, canvas, 1, 2, 8, "dataset:v1", 256, 256);
                    Assert.That(bitmap.Width, Is.EqualTo(256));
                }
                TileCache.Prune(directory, 0);
            })));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    [TestCase(null, "")]
    [TestCase("dataset-a:v1", "dataset-b:v1")]
    [TestCase("dataset:v1", "dataset:v2")]
    public async Task RenderCachedPng_IsolatesRevisionsAndReusesMatchingRevision(string firstRevision, string secondRevision)
    {
        var style = CreateLineStyle();
        style.SetSourceProvider("tiles", new StubVectorTileSource(CreateLineTile()));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "png-revision-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var canvas = new SkiaCanvas();
            var first = await Renderer.RenderCachedPng(directory, style, canvas, 1, 2, 8, firstRevision, 256, 256);
            var tile = CreateLineTile();
            tile.Layers[0].Features[0].Geometry = [[new Point(0, 0.5), new Point(1, 0.5)]];
            style.SetSourceProvider("tiles", new StubVectorTileSource(tile));
            var second = await Renderer.RenderCachedPng(directory, style, canvas, 1, 2, 8, secondRevision, 256, 256);
            using var firstImage = SKBitmap.Decode(first);
            using var secondImage = SKBitmap.Decode(second);
            Assert.That(firstImage.Pixels.SequenceEqual(secondImage.Pixels), Is.False);
            var shouldNotRender = new RecordingCanvas { FinishFailure = new InvalidOperationException("Unexpected miss") };
            Assert.That(await Renderer.RenderCachedPng(directory, style, shouldNotRender, 1, 2, 8, secondRevision, 256, 256), Is.EqualTo(second));
            Assert.That(shouldNotRender.FinishCalled, Is.False);
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(2));
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static Style CreateLineStyle()
    {
        var path = TestAssets.WriteTemporaryStyle("""
            {
              "sources": { "tiles": { "type": "vector" } },
              "layers": [
                {
                  "id": "roads",
                  "type": "line",
                  "source": "tiles",
                  "source-layer": "roads",
                  "paint": { "line-color": "#000000", "line-width": 1 }
                }
              ]
            }
            """);
        return new Style(path);
    }

    private static VectorTile CreateLineTile()
    {
        return new VectorTile
        {
            Layers =
            [
                new VectorTileLayer
                {
                    Name = "roads",
                    Features =
                    [
                        new VectorTileFeature
                        {
                            Extent = 1,
                            GeometryType = "LineString",
                            Geometry = [[new Point(0, 0), new Point(1, 1)]]
                        }
                    ]
                }
            ]
        };
    }

    private sealed class StubVectorTileSource(VectorTile tile) : IVectorTileSource
    {
        public Task<Stream> GetTile(int x, int y, int z) => Task.FromResult<Stream>(null);

        public Task<VectorTile> GetVectorTile(int x, int y, int z) => Task.FromResult(tile);
    }

    private sealed class RecordingCanvas : ICanvas
    {
        public bool ClipOverflow { get; set; }
        public List<List<Point>> LineStrings { get; } = [];
        public bool FinishCalled { get; private set; }
        public Exception FinishFailure { get; set; }
        public SKBitmap LastBitmap { get; private set; }

        public void StartDrawing(double sizeX, double sizeY) { }
        public void DrawBackground(Brush style) { }
        public void DrawLineString(List<Point> geometry, Brush style) => LineStrings.Add(geometry);
        public void DrawPolygon(List<Point> geometry, Brush style) { }
        public void DrawPoint(Point geometry, Brush style) { }
        public void DrawText(Point geometry, Brush style) { }
        public void DrawTextOnPath(List<Point> geometry, Brush style) { }
        public List<bool> ReadableRasters { get; } = new();
        public void DrawImage(Stream imageStream, Brush style) => ReadableRasters.Add(imageStream.CanRead);
        public void DrawUnknown(List<List<Point>> geometry, Brush style) { }

        public SKBitmap FinishDrawing()
        {
            FinishCalled = true;
            if (FinishFailure != null)
            {
                throw FinishFailure;
            }
            LastBitmap = new SKBitmap(1, 1);
            return LastBitmap;
        }
    }
}
