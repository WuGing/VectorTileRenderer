using System.Reflection;
using SkiaSharp;

namespace VectorTileRenderer.Tests;

[NonParallelizable]
public class CanvasPathTests
{
    private static readonly Func<List<Point>, SKPath> build = typeof(SkiaCanvas)
        .GetMethod("GetPathFromGeometry", BindingFlags.Static | BindingFlags.NonPublic)
        .CreateDelegate<Func<List<Point>, SKPath>>();

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(8)]
    [TestCase(128)]
    [TestCase(129)]
    [TestCase(4096)]
    public void PathConstruction_PreservesLegacyPointsVerbsAndFillType(int count)
    {
        var points = Enumerable.Range(0, count).Select(i => new Point(i * 0.125, Math.Sin(i) * 512)).ToList();
        if (count > 2) points[^1] = points[0];
        using var expected = LegacyPath(points);
        using var actual = build(points);
        Assert.That(actual.FillType, Is.EqualTo(expected.FillType));
        Assert.That(actual.VerbCount, Is.EqualTo(expected.VerbCount));
        Assert.That(actual.Points, Is.EqualTo(expected.Points));
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.MaxValue)]
    public void PathConstruction_PreservesDrawingForNonfiniteOrOverflowingCoordinates(double coordinate)
    {
        var geometry = new List<Point> { new(2, 2), new(coordinate, 8), new(20, 20), new(2, 2) };
        using var legacyPath = LegacyPath(geometry);
        using var actualPath = build(geometry);
        using var expected = new SKBitmap(32, 32);
        using var actual = new SKBitmap(32, 32);
        using var expectedCanvas = new SKCanvas(expected);
        using var actualCanvas = new SKCanvas(actual);
        expectedCanvas.Clear(SKColors.White);
        actualCanvas.Clear(SKColors.White);
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = SKColors.Blue, IsAntialias = true };
        expectedCanvas.DrawPath(legacyPath, paint);
        actualCanvas.DrawPath(actualPath, paint);
        Assert.That(actual.Pixels, Is.EqualTo(expected.Pixels));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LineAndPolygonDrawing_PreservePixelsWithClippingAndRepeatedVertices(bool clipped)
    {
        var points = new List<Point> { new(-20, 20), new(30, 12), new(30, 12), new(110, 45), new(70, 100), new(-20, 20) };
        var brush = new Brush
        {
            Paint = new Paint
            {
                LineColor = Color.FromRgb(20, 50, 190), LineOpacity = 0.6, LineWidth = 3,
                LineCap = PenLineCap.Round, LineDashArray = [4, 2],
                FillColor = Color.FromRgb(220, 30, 80), FillOpacity = 0.45
            }
        };
        using var actualCanvas = new SkiaCanvas { ClipOverflow = clipped };
        actualCanvas.StartDrawing(96, 96);
        actualCanvas.DrawBackground(new Brush { Paint = new Paint { BackgroundColor = Color.FromRgb(255, 255, 255) } });
        using var expected = new SKBitmap(96, 96);
        using var expectedCanvas = new SKCanvas(expected);
        expectedCanvas.Clear(SKColors.White);
        var line = clipped ? (List<Point>)typeof(SkiaCanvas).GetMethod("ClipLine", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(actualCanvas, [points]) : points;
        if (line != null)
        {
            using var path = LegacyPath(line);
            using var effect = SKPathEffect.CreateDash([4, 2], 0);
            using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeWidth = 3,
                Color = new SKColor(20, 50, 190, (byte)(255 * 0.6)), IsAntialias = true, PathEffect = effect };
            expectedCanvas.DrawPath(path, paint);
        }
        actualCanvas.DrawLineString(points, brush);
        var polygons = clipped ? (List<List<Point>>)typeof(SkiaCanvas).GetMethod("ClipPolygon", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(actualCanvas, [points]) : new List<List<Point>> { points };
        foreach (var polygon in polygons)
        {
            using var path = LegacyPath(polygon);
            using var paint = new SKPaint { Style = SKPaintStyle.Fill, StrokeCap = SKStrokeCap.Round,
                Color = new SKColor(220, 30, 80, (byte)(255 * 0.45)), IsAntialias = true };
            expectedCanvas.DrawPath(path, paint);
        }
        actualCanvas.DrawPolygon(points, brush);
        using var actual = actualCanvas.FinishDrawing();
        Assert.That(actual.Pixels, Is.EqualTo(expected.Pixels));
    }

    [Test]
    public void Drawing_ReusesScratchPathAndReleasesItOnFinish()
    {
        using var canvas = new SkiaCanvas();
        canvas.StartDrawing(32, 32);
        var geometry = new List<Point> { new(1, 1), new(20, 1), new(20, 20) };
        var brush = new Brush { Paint = new Paint() };
        canvas.DrawPolygon(geometry, brush);
        var path = OwnedPath(canvas);
        canvas.DrawLineString(geometry, brush);
        Assert.That(OwnedPath(canvas), Is.SameAs(path));
        using var bitmap = canvas.FinishDrawing();
        Assert.That(path.Handle, Is.EqualTo(IntPtr.Zero));
        Assert.That(bitmap.Handle, Is.Not.EqualTo(IntPtr.Zero));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ScratchPath_ObservesGeometryEditsAndReleasesOnRestartOrDispose(bool restart)
    {
        using var canvas = new SkiaCanvas();
        canvas.StartDrawing(64, 64);
        var geometry = new List<Point> { new(3, 3), new(50, 3), new(50, 50), new(3, 3) };
        var brush = new Brush { Paint = new Paint { LineWidth = 2, LineColor = Color.FromRgb(20, 40, 180) } };
        canvas.DrawLineString(geometry, brush);
        var first = OwnedPath(canvas);
        canvas.DrawPolygon(geometry, brush);
        Assert.That(OwnedPath(canvas), Is.SameAs(first));
        geometry[1] = new Point(8, 40);
        canvas.DrawLineString(geometry, brush);
        Assert.That(first.Points[1], Is.EqualTo(new SKPoint(8, 40)));
        geometry.Add(new Point(60, 5));
        canvas.DrawPolygon(geometry, brush);
        Assert.That(first.PointCount, Is.EqualTo(geometry.Count));
        if (restart) canvas.StartDrawing(64, 64);
        else canvas.Dispose();
        Assert.That(first.Handle, Is.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void Drawing_PreservesPixelsAcrossMutationAndClipChanges()
    {
        using var actualCanvas = new SkiaCanvas();
        actualCanvas.StartDrawing(64, 64);
        var geometry = new List<Point> { new(-12, 5), new(60, 3), new(25, 54), new(-12, 5) };
        using var expected = new SKBitmap(64, 64);
        using var expectedCanvas = new SKCanvas(expected);
        expectedCanvas.Clear(SKColors.White);
        actualCanvas.DrawBackground(new Brush { Paint = new Paint { BackgroundColor = Color.FromRgb(255, 255, 255) } });
        for (var i = 0; i < 4; i++)
        {
            geometry[1] = new Point(60, 3 + i * 7);
            if (i == 2) geometry.Add(new Point(20, 20));
            actualCanvas.ClipOverflow = i >= 2;
            var brush = new Brush { Paint = new Paint
            {
                FillColor = Color.FromRgb(200, 30, 80), FillOpacity = 0.25,
                LineColor = Color.FromRgb(20, 40, 180), LineWidth = 2
            } };
            var line = actualCanvas.ClipOverflow
                ? (List<Point>)typeof(SkiaCanvas).GetMethod("ClipLine", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(actualCanvas, [geometry])
                : geometry;
            if (line != null)
            {
                using var path = LegacyPath(line);
                using var paint = new SKPaint { Style = SKPaintStyle.Stroke, IsAntialias = true, StrokeWidth = 2, Color = new SKColor(20, 40, 180) };
                expectedCanvas.DrawPath(path, paint);
            }
            var polygons = actualCanvas.ClipOverflow
                ? (List<List<Point>>)typeof(SkiaCanvas).GetMethod("ClipPolygon", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(actualCanvas, [geometry])
                : new List<List<Point>> { geometry };
            foreach (var polygon in polygons ?? [])
            {
                using var path = LegacyPath(polygon);
                using var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true, Color = new SKColor(200, 30, 80, (byte)(255 * 0.25)) };
                expectedCanvas.DrawPath(path, paint);
            }
            actualCanvas.DrawLineString(geometry, brush);
            actualCanvas.DrawPolygon(geometry, brush);
        }
        using var actual = actualCanvas.FinishDrawing();
        Assert.That(actual.Pixels, Is.EqualTo(expected.Pixels));
    }

    [Test]
    public void ScratchPath_ReleasesAfterFailedSubmission()
    {
        using var canvas = new SkiaCanvas();
        canvas.StartDrawing(32, 32);
        var geometry = new List<Point> { new(1, 1), new(20, 20) };
        canvas.DrawLineString(geometry, new Brush { Paint = new Paint() });
        var path = OwnedPath(canvas);
        geometry.Clear();
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.DrawLineString(geometry, new Brush { Paint = new Paint() }));
        canvas.Dispose();
        Assert.That(path.Handle, Is.EqualTo(IntPtr.Zero));
    }

    private static SKPath OwnedPath(SkiaCanvas canvas) => (SKPath)typeof(SkiaCanvas)
        .GetField("drawingPath", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(canvas);

    [TestCase(129)]
    [TestCase(4096)]
    public void ScratchPath_PreservesPixelsAfterLargeAndNonfiniteGeometry(int count)
    {
        using var canvas = new SkiaCanvas();
        canvas.StartDrawing(64, 64);
        canvas.DrawBackground(new Brush { Paint = new Paint { BackgroundColor = Color.FromRgb(255, 255, 255) } });
        using var expected = new SKBitmap(64, 64);
        using var expectedCanvas = new SKCanvas(expected);
        expectedCanvas.Clear(SKColors.White);
        var geometries = new[]
        {
            Enumerable.Range(0, count).Select(i => new Point(32 + Math.Cos(i * 0.1) * 20, 32 + Math.Sin(i * 0.1) * 20)).ToList(),
            new List<Point> { new(2, 2), new(double.NaN, 20), new(40, 40) },
            new List<Point> { new(2, 50), new(60, 50) }
        };
        var brush = new Brush { Paint = new Paint { LineColor = Color.FromRgb(20, 40, 180), LineWidth = 2 } };
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, IsAntialias = true, StrokeWidth = 2, Color = new SKColor(20, 40, 180) };
        foreach (var geometry in geometries)
        {
            using var path = LegacyPath(geometry);
            expectedCanvas.DrawPath(path, paint);
            canvas.DrawLineString(geometry, brush);
        }
        Assert.That(OwnedPath(canvas).PointCount, Is.EqualTo(2));
        using var actual = canvas.FinishDrawing();
        Assert.That(actual.Pixels, Is.EqualTo(expected.Pixels));
    }

    [Test]
    public void Drawing_ReflectsBrushAndDashEditsAcrossCanvasReuse()
    {
        using var actualCanvas = new SkiaCanvas();
        var brush = new Brush { Paint = new Paint { LineDashArray = [4, 2] } };
        for (var image = 0; image < 2; image++)
        {
            actualCanvas.StartDrawing(96, 144);
            using var expected = new SKBitmap(96, 144);
            using var expectedCanvas = new SKCanvas(expected);
            expectedCanvas.Clear(SKColors.White);
            actualCanvas.DrawBackground(new Brush { Paint = new Paint { BackgroundColor = Color.FromRgb(255, 255, 255) } });
            var widths = new[] { 6.0, double.PositiveInfinity, 3.0, double.NaN, 5.0, -1.0, 0.0, 2.0 };
            for (var i = 0; i < widths.Length; i++)
            {
                brush.Paint.LineWidth = widths[i];
                brush.Paint.LineCap = i % 2 == 0 ? PenLineCap.Round : PenLineCap.Flat;
                brush.Paint.LineColor = Color.FromRgb((byte)(i * 20), 40, 180);
                brush.Paint.LineOpacity = 0.25 + i % 3 * 0.25;
                if (i == 1) brush.Paint.LineDashArray[0] = 9;
                if (i == 3) brush.Paint.LineDashArray = [];
                if (i == 5) brush.Paint.LineDashArray = [2, 5];
                var geometry = new List<Point> { new(5, 8 + i * 16), new(80, 10 + i * 16) };
                using var path = LegacyPath(geometry);
                using var effect = brush.Paint.LineDashArray.Length > 0
                    ? SKPathEffect.CreateDash(brush.Paint.LineDashArray.Select(value => (float)value).ToArray(), 0) : null;
                using var paint = new SKPaint { Style = SKPaintStyle.Stroke, IsAntialias = true,
                    StrokeCap = i % 2 == 0 ? SKStrokeCap.Round : SKStrokeCap.Butt, StrokeWidth = (float)widths[i],
                    Color = new SKColor((byte)(i * 20), 40, 180, (byte)(255 * brush.Paint.LineOpacity)), PathEffect = effect };
                expectedCanvas.DrawPath(path, paint);
                actualCanvas.DrawLineString(geometry, brush);
                brush.Paint.FillColor = Color.FromRgb(200, (byte)(i * 20), 80);
                brush.Paint.FillOpacity = brush.Paint.LineOpacity;
                var polygon = new List<Point> { new(20, 3 + i * 16), new(35, 3 + i * 16), new(30, 14 + i * 16) };
                using var polygonPath = LegacyPath(polygon);
                using var fill = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true,
                    Color = new SKColor(200, (byte)(i * 20), 80, (byte)(255 * brush.Paint.FillOpacity)) };
                expectedCanvas.DrawPath(polygonPath, fill);
                actualCanvas.DrawPolygon(polygon, brush);
            }
            var linePaint = OwnedPaint(actualCanvas, "linePaint");
            var polygonPaint = OwnedPaint(actualCanvas, "polygonPaint");
            using var actual = actualCanvas.FinishDrawing();
            Assert.That(actual.Pixels, Is.EqualTo(expected.Pixels));
            Assert.That(linePaint.Handle, Is.EqualTo(IntPtr.Zero));
            Assert.That(polygonPaint.Handle, Is.EqualTo(IntPtr.Zero));
        }
    }

    [Test]
    public void Dispose_ReleasesPaintsWhenDrawingIsInterrupted()
    {
        var canvas = new SkiaCanvas();
        canvas.StartDrawing(32, 32);
        var brush = new Brush { Paint = new Paint { LineWidth = 2, LineColor = Color.FromRgb(1, 2, 3), FillColor = Color.FromRgb(4, 5, 6) } };
        canvas.DrawLineString([new(1, 1), new(20, 20)], brush);
        canvas.DrawPolygon([new(1, 1), new(20, 1), new(20, 20)], brush);
        var linePaint = OwnedPaint(canvas, "linePaint");
        var polygonPaint = OwnedPaint(canvas, "polygonPaint");
        canvas.Dispose();
        Assert.That(linePaint.Handle, Is.EqualTo(IntPtr.Zero));
        Assert.That(polygonPaint.Handle, Is.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void Drawing_ObservesDashEditsWhenOtherPaintValuesAreIdentical()
    {
        using var canvas = new SkiaCanvas();
        canvas.StartDrawing(96, 48);
        canvas.DrawBackground(new Brush { Paint = new Paint { BackgroundColor = Color.FromRgb(255, 255, 255) } });
        using var expected = new SKBitmap(96, 48);
        using var expectedCanvas = new SKCanvas(expected);
        expectedCanvas.Clear(SKColors.White);
        var brush = new Brush { Paint = new Paint { LineColor = Color.FromRgb(20, 40, 180), LineWidth = 3, LineDashArray = [4, 2] } };
        for (var i = 0; i < 3; i++)
        {
            if (i == 1) brush.Paint.LineDashArray[0] = 9;
            if (i == 2) brush.Paint.LineDashArray = [];
            var geometry = new List<Point> { new(3, 8 + i * 16), new(90, 8 + i * 16) };
            using var path = LegacyPath(geometry);
            using var effect = brush.Paint.LineDashArray.Length > 0
                ? SKPathEffect.CreateDash(brush.Paint.LineDashArray.Select(value => (float)value).ToArray(), 0) : null;
            using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true,
                Color = new SKColor(20, 40, 180), PathEffect = effect };
            expectedCanvas.DrawPath(path, paint);
            canvas.DrawLineString(geometry, brush);
        }
        using var actual = canvas.FinishDrawing();
        Assert.That(actual.Pixels, Is.EqualTo(expected.Pixels));
    }

    private static SKPaint OwnedPaint(SkiaCanvas canvas, string name) => (SKPaint)typeof(SkiaCanvas)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(canvas);

    private static SKPath LegacyPath(List<Point> geometry)
    {
        var path = new SKPath { FillType = SKPathFillType.EvenOdd };
        path.MoveTo((float)geometry[0].X, (float)geometry[0].Y);
        foreach (var point in geometry.Skip(1)) path.LineTo((float)point.X, (float)point.Y);
        return path;
    }
}
