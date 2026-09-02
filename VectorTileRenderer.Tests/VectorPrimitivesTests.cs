using NUnit.Framework;

namespace VectorTileRenderer.Tests;

[TestFixture]
public class VectorPrimitivesTests
{
    [Test]
    public void RectIntersection_TreatsTouchingEdgesAsIntersecting()
    {
        var left = new Rect(0, 0, 10, 10);
        var right = new Rect(10, 2, 5, 5);

        Assert.That(left.IntersectsWith(right), Is.True);
    }

    [Test]
    public void Inflate_ExpandsAroundTheExistingCenter()
    {
        var rectangle = new Rect(10, 20, 30, 40);

        rectangle.Inflate(2, 3);

        Assert.Multiple(() =>
        {
            Assert.That(rectangle.Left, Is.EqualTo(8));
            Assert.That(rectangle.Top, Is.EqualTo(17));
            Assert.That(rectangle.Width, Is.EqualTo(34));
            Assert.That(rectangle.Height, Is.EqualTo(46));
        });
    }

    [Test]
    public void PointEquality_IsValueBased()
    {
        var first = new Point(1.25, -3.5);
        var second = new Point(1.25, -3.5);

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
    }
}
