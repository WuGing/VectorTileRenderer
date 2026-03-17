using System;

namespace VectorTileRenderer
{
    public readonly struct Vector2D(double x, double y)
    {
        public double X { get; } = x;
        public double Y { get; } = y;
        public double Length => Math.Sqrt(X * X + Y * Y);
    }

    public struct Point(double x, double y) : IEquatable<Point>
    {
        public double X { get; set; } = x;
        public double Y { get; set; } = y;

        public static Vector2D operator -(Point left, Point right)
        {
            return new Vector2D(left.X - right.X, left.Y - right.Y);
        }

        public readonly bool Equals(Point other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override readonly bool Equals(object obj)
        {
            return obj is Point other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 23) + X.GetHashCode();
                hash = (hash * 23) + Y.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(Point left, Point right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Point left, Point right)
        {
            return !left.Equals(right);
        }
    }

    public struct Rect(double left, double top, double width, double height)
    {
        public Rect(Point topLeft, Point bottomRight)
            : this(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y)
        {
        }

        public double Left { get; set; } = left;
        public double Top { get; set; } = top;
        public double Width { get; set; } = width;
        public double Height { get; set; } = height;

        public readonly double Right => Left + Width;
        public readonly double Bottom => Top + Height;

        public readonly Point TopLeft => new(Left, Top);
        public readonly Point TopRight => new(Right, Top);
        public readonly Point BottomLeft => new(Left, Bottom);
        public readonly Point BottomRight => new(Right, Bottom);

        public readonly bool IntersectsWith(Rect other)
        {
            return !(other.Left > Right || other.Right < Left || other.Top > Bottom || other.Bottom < Top);
        }

        public readonly bool Contains(Rect other)
        {
            return other.Left >= Left && other.Right <= Right && other.Top >= Top && other.Bottom <= Bottom;
        }

        public void Inflate(double width, double height)
        {
            Left -= width;
            Top -= height;
            Width += width * 2;
            Height += height * 2;
        }
    }

    public struct Color
    {
        public static readonly Color White = FromRgb(255, 255, 255);

        public byte A { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public static Color FromArgb(byte a, byte r, byte g, byte b)
        {
            return new Color { A = a, R = r, G = g, B = b };
        }

        public static Color FromRgb(byte r, byte g, byte b)
        {
            return new Color { A = 255, R = r, G = g, B = b };
        }
    }

    public enum PenLineCap
    {
        Flat,
        Square,
        Round
    }

    public enum TextAlignment
    {
        Left,
        Center,
        Right
    }
}
