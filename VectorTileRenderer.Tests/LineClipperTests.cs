using NUnit.Framework;

namespace VectorTileRenderer.Tests;

[TestFixture]
public class LineClipperTests
{
    private static readonly Rect Bounds = new(0, 0, 10, 10);

    [Test]
    public void ClipSegment_ClipsBothEndsAtRectangleEdges()
    {
        var segment = LineClipper.ClipSegment(Bounds, new Point(-5, 5), new Point(15, 5));

        Assert.Multiple(() =>
        {
            Assert.That(segment, Is.Not.Null);
            Assert.That(segment.Item1, Is.EqualTo(new Point(0, 5)));
            Assert.That(segment.Item2, Is.EqualTo(new Point(10, 5)));
        });
    }

    [Test]
    public void ClipSegment_ReturnsNull_WhenSegmentDoesNotIntersect()
    {
        var segment = LineClipper.ClipSegment(Bounds, new Point(-5, -5), new Point(-1, -1));

        Assert.That(segment, Is.Null);
    }

    [Test]
    public void ClipPolyline_PreservesConnectedSegments()
    {
        var line = new List<Point>
        {
            new(-5, 5),
            new(5, 5),
            new(15, 5)
        };

        var clipped = LineClipper.ClipPolyline(line, Bounds);

        Assert.That(clipped, Is.EqualTo(new[]
        {
            new Point(0, 5),
            new Point(5, 5),
            new Point(10, 5)
        }));
    }
}
