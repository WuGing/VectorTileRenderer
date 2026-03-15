using Clipper2Lib;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VectorTileRenderer
{
    public class SkiaCanvas : ICanvas
    {
        int width;
        int height;

        SKBitmap bitmap;
        SKSurface surface;
        SKCanvas canvas;

        public bool ClipOverflow { get; set; } = false;
        private Rect clipRectangle;
        Path64 clipRectanglePath;

        ConcurrentDictionary<string, SKTypeface> fontPairs = new ConcurrentDictionary<string, SKTypeface>();
        private static readonly Object fontLock = new Object();

        List<Rect> textRectangles = new List<Rect>();

        public void StartDrawing(double width, double height)
        {
            this.width = (int)width;
            this.height = (int)height;

            var info = new SKImageInfo(this.width, this.height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            bitmap = new SKBitmap(info);

            //var glInterface = GRGlInterface.CreateNativeGlInterface();
            //grContext = GRContext.Create(GRBackend.OpenGL, glInterface);

            //renderTarget = SkiaGL.CreateRenderTarget();
            //renderTarget.Width = this.width;
            //renderTarget.Height = this.height;


            surface = SKSurface.Create(info, bitmap.GetPixels(), bitmap.RowBytes);
            //surface = SKSurface.Create(grContext, renderTarget);
            canvas = surface.Canvas;

            double padding = -5;
            clipRectangle = new Rect(padding, padding, this.width - padding * 2, this.height - padding * 2);

            clipRectanglePath =
            [
                new Point64((long)clipRectangle.Top, (long)clipRectangle.Left),
                new Point64((long)clipRectangle.Top, (long)clipRectangle.Right),
                new Point64((long)clipRectangle.Bottom, (long)clipRectangle.Right),
                new Point64((long)clipRectangle.Bottom, (long)clipRectangle.Left),
            ];

            //clipRectanglePath = new List<IntPoint>();
            //clipRectanglePath.Add(new IntPoint((int)clipRectangle.Top + 10, (int)clipRectangle.Left + 10));
            //clipRectanglePath.Add(new IntPoint((int)clipRectangle.Top + 10, (int)clipRectangle.Right - 10));
            //clipRectanglePath.Add(new IntPoint((int)clipRectangle.Bottom - 10, (int)clipRectangle.Right - 10));
            //clipRectanglePath.Add(new IntPoint((int)clipRectangle.Bottom - 10, (int)clipRectangle.Left + 10));
        }

        public void DrawBackground(Brush style)
        {
            canvas.Clear(new SKColor(style.Paint.BackgroundColor.R, style.Paint.BackgroundColor.G, style.Paint.BackgroundColor.B, style.Paint.BackgroundColor.A));
        }

        SKStrokeCap convertCap(PenLineCap cap)
        {
            if (cap == PenLineCap.Flat)
            {
                return SKStrokeCap.Butt;
            }
            else if (cap == PenLineCap.Round)
            {
                return SKStrokeCap.Round;
            }

            return SKStrokeCap.Square;
        }

        //private double getAngle(double x1, double y1, double x2, double y2)
        //{
        //    double degrees;

        //    if (x2 - x1 == 0)
        //    {
        //        if (y2 > y1)
        //            degrees = 90;
        //        else
        //            degrees = 270;
        //    }
        //    else
        //    {
        //        // Calculate angle from offset.
        //        double riseoverrun = (y2 - y1) / (x2 - x1);
        //        double radians = Math.Atan(riseoverrun);
        //        degrees = radians * (180 / Math.PI);

        //        if ((x2 - x1) < 0 || (y2 - y1) < 0)
        //            degrees += 180;
        //        if ((x2 - x1) > 0 && (y2 - y1) < 0)
        //            degrees -= 180;
        //        if (degrees < 0)
        //            degrees += 360;
        //    }
        //    return degrees;
        //}

        //private double getAngleAverage(double a, double b)
        //{
        //    a = a % 360;
        //    b = b % 360;

        //    double sum = a + b;
        //    if (sum > 360 && sum < 540)
        //    {
        //        sum = sum % 180;
        //    }
        //    return sum / 2;
        //}

        double clamp(double number, double min = 0, double max = 1)
        {
            return Math.Max(min, Math.Min(max, number));
        }

        List<List<Point>> clipPolygon(List<Point> geometry) // may break polygons into multiple ones
        {
            var polygon = new Path64();

            foreach (var point in geometry)
            {
                polygon.Add(new Point64((long)point.X, (long)point.Y));
            }

            var subject = new Paths64() { polygon };
            var clip = new Paths64() { clipRectanglePath };
            var solution = Clipper.Intersect(subject, clip, FillRule.NonZero);

            if (solution.Count > 0)
            {
                var result = solution.Select(s => s.Select(item => new Point(item.X, item.Y)).ToList()).ToList();
                return result;
            }

            return null;
        }

        List<Point> clipLine(List<Point> geometry)
        {
            return LineClipper.ClipPolyline(geometry, clipRectangle);
        }

        SKPath getPathFromGeometry(List<Point> geometry)
        {

            SKPath path = new SKPath
            {
                FillType = SKPathFillType.EvenOdd,
            };

            var firstPoint = geometry[0];

            path.MoveTo((float)firstPoint.X, (float)firstPoint.Y);
            foreach (var point in geometry.Skip(1))
            {
                var lastPoint = path.LastPoint;
                path.LineTo((float)point.X, (float)point.Y);
            }

            return path;
        }

        public void DrawLineString(List<Point> geometry, Brush style)
        {
            if (ClipOverflow)
            {
                geometry = clipLine(geometry);
                if (geometry == null)
                {
                    return;
                }
            }

            var path = getPathFromGeometry(geometry);
            if (path == null)
            {
                return;
            }

            SKPaint fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeCap = convertCap(style.Paint.LineCap),
                StrokeWidth = (float)style.Paint.LineWidth,
                Color = new SKColor(style.Paint.LineColor.R, style.Paint.LineColor.G, style.Paint.LineColor.B, (byte)clamp(style.Paint.LineColor.A * style.Paint.LineOpacity, 0, 255)),
                IsAntialias = true,
            };

            if (style.Paint.LineDashArray.Count() > 0)
            {
                var effect = SKPathEffect.CreateDash(style.Paint.LineDashArray.Select(n => (float)n).ToArray(), 0);
                fillPaint.PathEffect = effect;
            }

            //Console.WriteLine(style.Paint.LineWidth);

            canvas.DrawPath(path, fillPaint);
        }

        SKTextAlign convertAlignment(TextAlignment alignment)
        {
            if (alignment == TextAlignment.Center)
            {
                return SKTextAlign.Center;
            }
            else if (alignment == TextAlignment.Left)
            {
                return SKTextAlign.Left;
            }
            else if (alignment == TextAlignment.Right)
            {
                return SKTextAlign.Right;
            }

            return SKTextAlign.Center;
        }

        SKPaint getTextStrokePaint(Brush style)
        {
            var paint = new SKPaint()
            {
                IsStroke = true,
                StrokeWidth = (float)style.Paint.TextStrokeWidth,
                Color = new SKColor(style.Paint.TextStrokeColor.R, style.Paint.TextStrokeColor.G, style.Paint.TextStrokeColor.B, (byte)clamp(style.Paint.TextStrokeColor.A * style.Paint.TextOpacity, 0, 255)),
                IsAntialias = true,
            };

            return paint;
        }

        SKFont getTextFont(Brush style, SKTypeface typeface = null)
        {
            return new SKFont(typeface ?? getFont(style.Paint.TextFont, style), (float)style.Paint.TextSize)
            {
                Hinting = SKFontHinting.Normal,
            };
        }

        SKPaint getTextPaint(Brush style)
        {
            var paint = new SKPaint()
            {
                Color = new SKColor(style.Paint.TextColor.R, style.Paint.TextColor.G, style.Paint.TextColor.B, (byte)clamp(style.Paint.TextColor.A * style.Paint.TextOpacity, 0, 255)),
                IsAntialias = true,
            };

            return paint;
        }

        string applyTextTransform(string text, Brush style)
        {
            if (text.Length == 0)
            {
                return "";
            }

            if (style.Paint.TextTransform == TextTransform.Uppercase)
            {
                text = text.ToUpper();
            }
            else if (style.Paint.TextTransform == TextTransform.Lowercase)
            {
                text = text.ToLower();
            }

            return text;
        }

        string transformText(string text, Brush style, SKPaint paint, SKFont font)
        {
            text = applyTextTransform(text, style);

            text = breakText(text, paint, font, style);

            return text;
            //return Encoding.UTF32.GetBytes(newText);
        }

        string transformTextSingleLine(string text, Brush style)
        {
            text = applyTextTransform(text, style);
            return text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        int breakTextLength(string text, float maxWidth, SKPaint paint, SKFont font)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            if (font.MeasureText(text, paint) <= maxWidth)
            {
                return text.Length;
            }

            int low = 1;
            int high = text.Length;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                var candidate = text.Substring(0, mid);
                if (font.MeasureText(candidate, paint) <= maxWidth)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return Math.Max(1, high);
        }

        string breakText(string input, SKPaint paint, SKFont font, Brush style)
        {
            var restOfText = input;
            var brokenText = "";
            var maxWidth = (float)(style.Paint.TextMaxWidth * style.Paint.TextSize);
            do
            {
                var lineLength = breakTextLength(restOfText, maxWidth, paint, font);

                if (lineLength == restOfText.Length)
                {
                    // its the end
                    brokenText += restOfText.Trim();
                    break;
                }

                var lastSpaceIndex = restOfText.LastIndexOf(' ', (int)(lineLength - 1));
                if (lastSpaceIndex == -1 || lastSpaceIndex == 0)
                {
                    // no more spaces, probably ;)
                    brokenText += restOfText.Trim();
                    break;
                }

                brokenText += restOfText.Substring(0, (int)lastSpaceIndex).Trim() + "\n";

                restOfText = restOfText.Substring((int)lastSpaceIndex, restOfText.Length - (int)lastSpaceIndex);

            } while (restOfText.Length > 0);

            return brokenText.Trim();
        }

        bool textCollides(Rect rectangle)
        {
            foreach (var rect in textRectangles)
            {
                if (rect.IntersectsWith(rectangle))
                {
                    return true;
                }
            }
            return false;
        }

        SKTypeface getFont(string[] familyNames, Brush style)
        {
            lock (fontLock)
            {
                foreach (var name in familyNames)
                {
                    if (fontPairs.ContainsKey(name))
                    {
                        return fontPairs[name];
                    }

                    if (style.GlyphsDirectory != null)
                    {
                        // check file system for ttf
                        var newType = SKTypeface.FromFile(System.IO.Path.Combine(style.GlyphsDirectory, name + ".ttf"));
                        if (newType != null)
                        {
                            fontPairs[name] = newType;
                            return newType;
                        }

                        // check file system for otf
                        newType = SKTypeface.FromFile(System.IO.Path.Combine(style.GlyphsDirectory, name + ".otf"));
                        if (newType != null)
                        {
                            fontPairs[name] = newType;
                            return newType;
                        }
                    }

                    var typeface = SKTypeface.FromFamilyName(name);
                    if (typeface.FamilyName == name)
                    {
                        // gotcha!
                        fontPairs[name] = typeface;
                        return typeface;
                    }
                }

                // all options exhausted...
                // get the first one
                var fallback = SKTypeface.FromFamilyName(familyNames.First());
                fontPairs[familyNames.First()] = fallback;
                return fallback;
            }
        }

        SKTypeface qualifyTypeface(string text, SKTypeface typeface)
        {
            var glyphs = new ushort[typeface.CountGlyphs(text)];
            if (glyphs.Length < text.Length)
            {
                var fm = SKFontManager.Default;
                var charIdx = (glyphs.Length > 0) ? glyphs.Length : 0;
                return fm.MatchCharacter(text[glyphs.Length]);
            }

            return typeface;
        }

        void qualifyTypeface(Brush style, SKFont font)
        {
            var typeface = font.Typeface;
            if (typeface == null)
            {
                return;
            }

            var glyphs = new ushort[typeface.CountGlyphs(style.Text)];
            if (glyphs.Length < style.Text.Length)
            {
                var fm = SKFontManager.Default;
                var charIdx = (glyphs.Length > 0) ? glyphs.Length : 0;
                var newTypeface = fm.MatchCharacter(style.Text[glyphs.Length]);

                if (newTypeface == null)
                {
                    return;
                }

                font.Typeface = newTypeface;

                glyphs = new ushort[newTypeface.CountGlyphs(style.Text)];
                if (glyphs.Length < style.Text.Length)
                {
                    // still causing issues
                    // so we cut the rest
                    charIdx = (glyphs.Length > 0) ? glyphs.Length : 0;

                    style.Text = style.Text.Substring(0, charIdx);
                }
            }

        }

        public void DrawText(Point geometry, Brush style)
        {
            if (style.Paint.TextOptional)
            {
                // TODO check symbol collision
                //return;
            }

            var paint = getTextPaint(style);
            var font = getTextFont(style);
            qualifyTypeface(style, font);
            var textAlign = convertAlignment(style.Paint.TextJustify);

            var strokePaint = getTextStrokePaint(style);
            var strokeFont = getTextFont(style, font.Typeface);
            var text = transformText(style.Text, style, paint, font);
            var allLines = text.Split('\n');

            //paint.Typeface = qualifyTypeface(text, paint.Typeface);

            // detect collisions
            if (allLines.Length > 0)
            {
                var biggestLine = allLines.OrderBy(line => line.Length).Last();
                var width = (int)(font.MeasureText(biggestLine, paint));
                int left = (int)(geometry.X - width / 2);
                int top = (int)(geometry.Y - style.Paint.TextSize / 2 * allLines.Length);
                int height = (int)(style.Paint.TextSize * allLines.Length);

                var rectangle = new Rect(left, top, width, height);
                rectangle.Inflate(5, 5);

                if (ClipOverflow)
                {
                    if (!clipRectangle.Contains(rectangle))
                    {
                        return;
                    }
                }

                if (textCollides(rectangle))
                {
                    // collision detected
                    return;
                }
                textRectangles.Add(rectangle);

                //var list = new List<Point>()
                //{
                //    rectangle.TopLeft,
                //    rectangle.TopRight,
                //    rectangle.BottomRight,
                //    rectangle.BottomLeft,
                //};

                //var brush = new Brush();
                //brush.Paint = new Paint();
                //brush.Paint.FillColor = Color.FromArgb(150, 255, 0, 0);

                //this.DrawPolygon(list, brush);
            }

            int i = 0;
            foreach (var line in allLines)
            {
                float lineOffset = (float)(i * style.Paint.TextSize) - ((float)(allLines.Length) * (float)style.Paint.TextSize) / 2 + (float)style.Paint.TextSize;
                var position = new SKPoint((float)geometry.X + (float)(style.Paint.TextOffset.X * style.Paint.TextSize), (float)geometry.Y + (float)(style.Paint.TextOffset.Y * style.Paint.TextSize) + lineOffset);

                if (style.Paint.TextStrokeWidth != 0)
                {
                    canvas.DrawText(line, position, textAlign, strokeFont, strokePaint);
                }

                canvas.DrawText(line, position, textAlign, font, paint);
                i++;
            }

        }

        double getPathLength(List<Point> path)
        {
            double distance = 0;
            for (var i = 0; i < path.Count - 2; i++)
            {
                distance += (path[i] - path[i + 1]).Length;
            }

            return distance;
        }

        double getAbsoluteDiff2Angles(double x, double y, double c = Math.PI)
        {
            return c - Math.Abs((Math.Abs(x - y) % 2 * c) - c);
        }

        bool checkPathSqueezing(List<Point> path, double textHeight)
        {
            //double maxCurve = 0;
            double previousAngle = 0;
            for (var i = 0; i < path.Count - 2; i++)
            {
                var vector = path[i] - path[i + 1];

                var angle = Math.Atan2(vector.Y, vector.X);
                var angleDiff = Math.Abs(getAbsoluteDiff2Angles(angle, previousAngle));

                //var length = vector.Length / textHeight;
                //var curve = angleDiff / length;
                //maxCurve = Math.Max(curve, maxCurve);


                if (angleDiff > Math.PI / 3)
                {
                    return true;
                }

                previousAngle = angle;
            }

            return false;

            //return 0;

            //return maxCurve;
        }

        void debugRectangle(Rect rectangle, Color color)
        {
            var list = new List<Point>()
            {
                rectangle.TopLeft,
                rectangle.TopRight,
                rectangle.BottomRight,
                rectangle.BottomLeft,
            };

            var brush = new Brush();
            brush.Paint = new Paint();
            brush.Paint.FillColor = color;

            this.DrawPolygon(list, brush);
        }

        public void DrawTextOnPath(List<Point> geometry, Brush style)
        {
            // buggggyyyyyy
            // requires an amazing collision system to work :/
            // --
            //return;

            //if (ClipOverflow)
            //{
            geometry = clipLine(geometry);
            if (geometry == null)
            {
                return;
            }
            //}

            var path = getPathFromGeometry(geometry);
            var textPaint = getTextPaint(style);
            var textFont = getTextFont(style);
            qualifyTypeface(style, textFont);
            var strokePaint = getTextStrokePaint(style);
            var strokeFont = getTextFont(style, textFont.Typeface);
            var pathTextAlign = SKTextAlign.Left;
            var text = transformTextSingleLine(style.Text, style);

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var textWidth = textFont.MeasureText(text, textPaint);
            if (textWidth <= 0)
            {
                return;
            }

            var pathSqueezed = checkPathSqueezing(geometry, style.Paint.TextSize);

            if (pathSqueezed)
            {
                return;
            }

            //text += " : " + bending.ToString("F");

            var bounds = path.Bounds;

            var left = bounds.Left - style.Paint.TextSize;
            var top = bounds.Top - style.Paint.TextSize;
            var right = bounds.Right + style.Paint.TextSize;
            var bottom = bounds.Bottom + style.Paint.TextSize;

            var rectangle = new Rect(left, top, right - left, bottom - top);

            if (textCollides(rectangle))
            {
                //debugRectangle(rectangle, Color.FromArgb(128, 100, 255, 100));
                // collides with other
                return;
            }
            textRectangles.Add(rectangle);

            //debugRectangle(rectangle, Color.FromArgb(150, 255, 0, 0));

            if (style.Text.Length * style.Paint.TextSize * 0.2 >= getPathLength(geometry))
            {
                // exceeds estimated path length
                return;
            }

            var horizontalOffset = (float)style.Paint.TextOffset.X;
            var verticalOffset = (float)style.Paint.TextOffset.Y;

            if (style.Paint.TextStrokeWidth != 0)
            {
                // TODO implement this func custom way...
                canvas.DrawTextOnPath(text, path, horizontalOffset, verticalOffset, pathTextAlign, strokeFont, strokePaint);
            }

            canvas.DrawTextOnPath(text, path, horizontalOffset, verticalOffset, pathTextAlign, textFont, textPaint);


            //canvas.DrawText(Encoding.UTF32.GetBytes(bending.ToString("F")), new SKPoint((float)left + 10, (float)top + 10), getTextStrokePaint(style));
            //canvas.DrawText(Encoding.UTF32.GetBytes(bending.ToString("F")), new SKPoint((float)left + 10, (float)top + 10), getTextPaint(style));
        }

        public void DrawPoint(Point geometry, Brush style)
        {
            if (style.Paint.IconImage != null)
            {
                // draw icon here
            }
        }

        public void DrawPolygon(List<Point> geometry, Brush style)
        {
            List<List<Point>> allGeometries = null;
            if (ClipOverflow)
            {
                allGeometries = clipPolygon(geometry);
            }
            else
            {
                allGeometries = new List<List<Point>>() { geometry };
            }

            if (allGeometries == null)
            {
                return;
            }

            foreach (var geometryPart in allGeometries)
            {
                var path = getPathFromGeometry(geometryPart);
                if (path == null)
                {
                    return;
                }

                SKPaint fillPaint = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    StrokeCap = convertCap(style.Paint.LineCap),
                    Color = new SKColor(style.Paint.FillColor.R, style.Paint.FillColor.G, style.Paint.FillColor.B, (byte)clamp(style.Paint.FillColor.A * style.Paint.FillOpacity, 0, 255)),
                    IsAntialias = true,
                };

                canvas.DrawPath(path, fillPaint);
            }

        }


        public void DrawImage(Stream imageStream, Brush style)
        {
            if (imageStream == null)
            {
                return;
            }

            imageStream.Position = 0;
            using (var image = SKBitmap.Decode(imageStream))
            {
                if (image == null)
                {
                    return;
                }

                canvas.DrawBitmap(image, new SKRect(0, 0, width, height));
            }
        }

        public void DrawUnknown(List<List<Point>> geometry, Brush style)
        {

        }

        public SKBitmap FinishDrawing()
        {
            //using (var paint = new SKPaint())
            //{
            //    paint.Color = new SKColor(255, 255, 255, 255);
            //    paint.Style = SKPaintStyle.Fill;
            //    paint.TextSize = 24;
            //    paint.IsAntialias = true;

            //    var bytes = Encoding.UTF32.GetBytes("HELLO WORLD");
            //    canvas.DrawText(bytes, new SKPoint(10, 10), paint);
            //}


            //surface.Canvas.Flush();
            //grContext.


            canvas.Flush();

            return bitmap;

        }
    }
}


