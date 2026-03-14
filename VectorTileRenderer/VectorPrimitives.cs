using System;

namespace VectorTileRenderer
{
    public readonly struct Vector2D
    {
        public Vector2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
        public double Length => Math.Sqrt(X * X + Y * Y);
    }

    public struct Point : IEquatable<Point>
    {
        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; set; }
        public double Y { get; set; }

        public static Vector2D operator -(Point left, Point right)
        {
            return new Vector2D(left.X - right.X, left.Y - right.Y);
        }

        public bool Equals(Point other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is Point other && Equals(other);
        }

        public override int GetHashCode()
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

    public struct Rect
    {
        public Rect(double left, double top, double width, double height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public Rect(Point topLeft, Point bottomRight)
            : this(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y)
        {
        }

        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public double Right => Left + Width;
        public double Bottom => Top + Height;

        public Point TopLeft => new Point(Left, Top);
        public Point TopRight => new Point(Right, Top);
        public Point BottomLeft => new Point(Left, Bottom);
        public Point BottomRight => new Point(Right, Bottom);

        public bool IntersectsWith(Rect other)
        {
            return !(other.Left > Right || other.Right < Left || other.Top > Bottom || other.Bottom < Top);
        }

        public bool Contains(Rect other)
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
