using Clipper2Lib;
using SkiaSharp;
using System.Linq;

namespace WuGing.VectorTileRenderer;

public class SkiaCanvas : ICanvas
{
    protected int width;
    protected int height;

    protected SKBitmap bitmap;
    protected SKSurface surface;
    protected SKCanvas canvas;

    public bool ClipOverflow { get; set; } = false;
    private Rect clipRectangle;
    private Path64 clipRectanglePath;

    private readonly ConcurrentDictionary<string, SKTypeface> fontPairs = new();
    private static readonly object fontLock = new();

    private readonly List<Rect> textRectangles = [];

    public virtual void StartDrawing(double width, double height)
    {
        this.width = (int)width;
        this.height = (int)height;

        var info = new SKImageInfo(this.width, this.height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        if (!TryCreateSurface(info, out surface, out bitmap))
        {
            bitmap = new SKBitmap(info);
            surface = SKSurface.Create(info, bitmap.GetPixels(), bitmap.RowBytes);
        }

        canvas = surface.Canvas;

        InitializeClipArea();
    }

    protected virtual bool TryCreateSurface(SKImageInfo info, out SKSurface createdSurface, out SKBitmap createdBitmap)
    {
        createdBitmap = null;
        createdSurface = null;
        return false;
    }

    protected virtual void OnBeforeFinishDrawing()
    {
    }

    private void InitializeClipArea()
    {
        textRectangles.Clear();

        double padding = -5;
        clipRectangle = new Rect(padding, padding, width - padding * 2, height - padding * 2);

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

    private static SKStrokeCap ConvertCap(PenLineCap cap) => cap switch
    {
        PenLineCap.Flat => SKStrokeCap.Butt,
        PenLineCap.Round => SKStrokeCap.Round,
        _ => SKStrokeCap.Square,
    };

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

    private static double Clamp(double number, double min = 0, double max = 1)
    {
        return Math.Max(min, Math.Min(max, number));
    }

    private List<List<Point>> ClipPolygon(List<Point> geometry) // may break polygons into multiple ones
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

    private List<Point> ClipLine(List<Point> geometry)
    {
        return LineClipper.ClipPolyline(geometry, clipRectangle);
    }

    private static SKPath GetPathFromGeometry(List<Point> geometry)
    {
        SKPath path = new()
        {
            FillType = SKPathFillType.EvenOdd,
        };

        var firstPoint = geometry[0];

        path.MoveTo((float)firstPoint.X, (float)firstPoint.Y);
        foreach (var point in geometry.Skip(1))
        {
            path.LineTo((float)point.X, (float)point.Y);
        }

        return path;
    }

    public void DrawLineString(List<Point> geometry, Brush style)
    {
        if (ClipOverflow)
        {
            geometry = ClipLine(geometry);
            if (geometry == null)
            {
                return;
            }
        }

        var path = GetPathFromGeometry(geometry);
        if (path == null)
        {
            return;
        }

        SKPaint fillPaint = new()
        {
            Style = SKPaintStyle.Stroke,
            StrokeCap = ConvertCap(style.Paint.LineCap),
            StrokeWidth = (float)style.Paint.LineWidth,
            Color = new SKColor(style.Paint.LineColor.R, style.Paint.LineColor.G, style.Paint.LineColor.B, (byte)Clamp(style.Paint.LineColor.A * style.Paint.LineOpacity, 0, 255)),
            IsAntialias = true,
        };

        if (style.Paint.LineDashArray.Length > 0)
        {
            var effect = SKPathEffect.CreateDash([.. style.Paint.LineDashArray.Select(n => (float)n)], 0);
            fillPaint.PathEffect = effect;
        }

        canvas.DrawPath(path, fillPaint);
    }

    private static SKTextAlign ConvertAlignment(TextAlignment alignment) => alignment switch
    {
        TextAlignment.Center => SKTextAlign.Center,
        TextAlignment.Left => SKTextAlign.Left,
        TextAlignment.Right => SKTextAlign.Right,
        _ => SKTextAlign.Center,
    };

    private static SKPaint GetTextStrokePaint(Brush style)
    {
        var paint = new SKPaint()
        {
            IsStroke = true,
            StrokeWidth = (float)style.Paint.TextStrokeWidth,
            Color = new SKColor(style.Paint.TextStrokeColor.R, style.Paint.TextStrokeColor.G, style.Paint.TextStrokeColor.B, (byte)Clamp(style.Paint.TextStrokeColor.A * style.Paint.TextOpacity, 0, 255)),
            IsAntialias = true,
        };

        return paint;
    }

    private SKFont GetTextFont(Brush style, SKTypeface typeface = null)
    {
        return new SKFont(typeface ?? GetFont(style.Paint.TextFont, style), (float)style.Paint.TextSize)
        {
            Hinting = SKFontHinting.Normal,
        };
    }

    private static SKPaint GetTextPaint(Brush style)
    {
        var paint = new SKPaint()
        {
            Color = new SKColor(style.Paint.TextColor.R, style.Paint.TextColor.G, style.Paint.TextColor.B, (byte)Clamp(style.Paint.TextColor.A * style.Paint.TextOpacity, 0, 255)),
            IsAntialias = true,
        };

        return paint;
    }

    private static string ApplyTextTransform(string text, Brush style)
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

    private static string TransformText(string text, Brush style, SKPaint paint, SKFont font)
    {
        text = ApplyTextTransform(text, style);

        text = BreakText(text, paint, font, style);

        return text;
    }

    private static string TransformTextSingleLine(string text, Brush style)
    {
        text = ApplyTextTransform(text, style);
        return text.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    private static int BreakTextLength(string text, float maxWidth, SKPaint paint, SKFont font)
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

    private static string BreakText(string input, SKPaint paint, SKFont font, Brush style)
    {
        var restOfText = input;
        var brokenText = string.Empty;
        var maxWidth = (float)(style.Paint.TextMaxWidth * style.Paint.TextSize);
        do
        {
            var lineLength = BreakTextLength(restOfText, maxWidth, paint, font);

            if (lineLength == restOfText.Length)
            {
                // its the end
                brokenText += restOfText.Trim();
                break;
            }

            var lastSpaceIndex = restOfText.LastIndexOf(' ', lineLength - 1);
            if (lastSpaceIndex == -1 || lastSpaceIndex == 0)
            {
                // no more spaces, probably ;)
                brokenText += restOfText.Trim();
                break;
            }

            brokenText += restOfText.Substring(0, lastSpaceIndex).Trim() + "\n";

            restOfText = restOfText.Substring(lastSpaceIndex);

        } while (restOfText.Length > 0);

        return brokenText.Trim();
    }

    private bool TextCollides(Rect rectangle)
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

    private SKTypeface GetFont(string[] familyNames, Brush style)
    {
        lock (fontLock)
        {
            foreach (var name in familyNames)
            {
                if (fontPairs.TryGetValue(name, out var typeface))
                {
                    return typeface;
                }

                if (style.GlyphsDirectory != null)
                {
                    // check file system for ttf
                    var newType = SKTypeface.FromFile(Path.Combine(style.GlyphsDirectory, name + ".ttf"));
                    if (newType != null)
                    {
                        fontPairs[name] = newType;
                        return newType;
                    }

                    // check file system for otf
                    newType = SKTypeface.FromFile(Path.Combine(style.GlyphsDirectory, name + ".otf"));
                    if (newType != null)
                    {
                        fontPairs[name] = newType;
                        return newType;
                    }
                }

                typeface = SKTypeface.FromFamilyName(name);
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

    private static SKTypeface QualifyTypeface(string text, SKTypeface typeface)
    {
        var glyphs = new ushort[typeface.CountGlyphs(text)];
        if (glyphs.Length < text.Length)
        {
            var fm = SKFontManager.Default;
            return fm.MatchCharacter(text[glyphs.Length]);
        }

        return typeface;
    }

    private static void QualifyTypeface(Brush style, SKFont font)
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
                int charIdx = (glyphs.Length > 0) ? glyphs.Length : 0;

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

        var paint = GetTextPaint(style);
        var font = GetTextFont(style);
        QualifyTypeface(style, font);
        var textAlign = ConvertAlignment(style.Paint.TextJustify);

        var strokePaint = GetTextStrokePaint(style);
        var strokeFont = GetTextFont(style, font.Typeface);
        var text = TransformText(style.Text, style, paint, font);
        var allLines = text.Split('\n');

        var positions = new SKPoint[allLines.Length];
        var labelBounds = SKRect.Empty;
        var hasInk = false;
        for (var i = 0; i < allLines.Length; i++)
        {
            var lineOffset = (i - allLines.Length / 2f + 1) * (float)style.Paint.TextSize;
            positions[i] = new SKPoint(
                (float)(geometry.X + style.Paint.TextOffset.X * style.Paint.TextSize),
                (float)(geometry.Y + style.Paint.TextOffset.Y * style.Paint.TextSize) + lineOffset);
            var advance = font.MeasureText(allLines[i], out var bounds, paint);
            if (bounds.IsEmpty) continue;
            var alignmentOffset = textAlign == SKTextAlign.Center ? advance / 2
                : textAlign == SKTextAlign.Right ? advance : 0;
            bounds.Offset(positions[i].X - alignmentOffset, positions[i].Y);
            labelBounds = hasInk ? SKRect.Union(labelBounds, bounds) : bounds;
            hasInk = true;
        }
        if (!hasInk) return;

        // Include halo and antialiasing at every zoom, not only overzoom.
        // These are the same baselines, alignment and offsets used for drawing.
        labelBounds.Inflate((float)Math.Abs(style.Paint.TextStrokeWidth) / 2 + 1,
            (float)Math.Abs(style.Paint.TextStrokeWidth) / 2 + 1);
        var rectangle = new Rect(labelBounds.Left, labelBounds.Top, labelBounds.Width, labelBounds.Height);
        if (!new Rect(0, 0, width, height).Contains(rectangle)) return;
        rectangle.Inflate(5, 5);
        if (TextCollides(rectangle)) return;
        textRectangles.Add(rectangle);

        for (var i = 0; i < allLines.Length; i++)
        {
            if (style.Paint.TextStrokeWidth != 0)
            {
                canvas.DrawText(allLines[i], positions[i], textAlign, strokeFont, strokePaint);
            }
            canvas.DrawText(allLines[i], positions[i], textAlign, font, paint);
        }
    }

    private static double GetPathLength(List<Point> path)
    {
        double distance = 0;
        for (var i = 1; i < path.Count; i++)
        {
            distance += (path[i] - path[i - 1]).Length;
        }
        return distance;
    }

    // Linear scan with no glyph-placement search. Partition at sharp turns or
    // excessive total bending, then retain the least-bent section that fits.
    private static List<Point> SelectLabelSection(List<Point> road, double requiredLength)
    {
        var start = 0;
        var bestStart = -1;
        var bestEnd = -1;
        var bestBend = double.MaxValue;
        var bestLength = 0d;
        var length = 0d;
        var bend = 0d;
        double? previousAngle = null;

        void Consider(int end)
        {
            if (length >= requiredLength && (bend < bestBend || (bend == bestBend && length > bestLength)))
            {
                bestStart = start;
                bestEnd = end;
                bestBend = bend;
                bestLength = length;
            }
        }

        for (var i = 1; i < road.Count; i++)
        {
            var vector = road[i] - road[i - 1];
            var segmentLength = vector.Length;
            if (segmentLength == 0)
            {
                continue;
            }
            var angle = Math.Atan2(vector.Y, vector.X);
            var difference = previousAngle.HasValue ? angle - previousAngle.Value : 0;
            var turn = Math.Abs(Math.Atan2(Math.Sin(difference), Math.Cos(difference)));
            if (turn > Math.PI / 12 || bend + turn > 35 * Math.PI / 180)
            {
                Consider(i - 1);
                start = i - 1;
                length = 0;
                bend = 0;
            }
            else
            {
                bend += turn;
            }
            length += segmentLength;
            previousAngle = angle;
        }
        Consider(road.Count - 1);
        return bestStart < 0 ? null : road.GetRange(bestStart, bestEnd - bestStart + 1);
    }

    private void DebugRectangle(Rect rectangle, Color color)
    {
        var list = new List<Point>()
        {
            rectangle.TopLeft,
            rectangle.TopRight,
            rectangle.BottomRight,
            rectangle.BottomLeft,
        };

        var brush = new Brush
        {
            Paint = new Paint
            {
                FillColor = color
            }
        };

        DrawPolygon(list, brush);
    }

    public void DrawTextOnPath(List<Point> geometry, Brush style)
    {
        var textPaint = GetTextPaint(style);
        var textFont = GetTextFont(style);
        QualifyTypeface(style, textFont);
        var text = TransformTextSingleLine(style.Text, style);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var textWidth = textFont.MeasureText(text, textPaint);
        var offset = new SKPoint((float)style.Paint.TextOffset.X, (float)style.Paint.TextOffset.Y);
        var margin = (float)Math.Abs(style.Paint.TextStrokeWidth) / 2 + 1;
        if (textWidth <= 0)
        {
            return;
        }

        // A road that leaves and re-enters the tile has separate visible runs.
        // Never invent a connecting segment between those runs.
        foreach (var run in ClipLabelRuns(geometry).OrderByDescending(GetPathLength))
        {
            var first = run[0];
            var last = run[run.Count - 1];
            if (first.X > last.X || (first.X == last.X && first.Y < last.Y))
            {
                run.Reverse();
            }

            var section = SelectLabelSection(run, textWidth + 2 * (Math.Abs(offset.X) + margin));
            if (section == null)
            {
                continue;
            }
            using var path = GetPathFromGeometry(section);
            // Position rigid glyphs at road tangents; DrawTextOnPath's default
            // warps glyph outlines and can severely deform letters on curves.
            using var label = SKTextBlob.CreatePathPositioned(text, textFont, path, SKTextAlign.Center, offset);
            if (label == null)
            {
                continue;
            }
            var bounds = label.Bounds;
            bounds.Inflate(margin, margin);
            var rectangle = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            if (!new Rect(0, 0, width, height).Contains(rectangle) || TextCollides(rectangle))
            {
                continue;
            }

            textRectangles.Add(rectangle);
            if (style.Paint.TextStrokeWidth != 0)
            {
                canvas.DrawText(label, 0, 0, GetTextStrokePaint(style));
            }
            canvas.DrawText(label, 0, 0, textPaint);
            return;
        }
    }

    private IEnumerable<List<Point>> ClipLabelRuns(List<Point> geometry)
    {
        List<Point> run = null;
        for (var i = 1; i < geometry.Count; i++)
        {
            var segment = LineClipper.ClipSegment(clipRectangle, geometry[i - 1], geometry[i]);
            if (segment == null)
            {
                if (run != null) yield return run;
                run = null;
                continue;
            }
            if (segment.Item1 == segment.Item2) continue;
            if (run == null || run[run.Count - 1] != segment.Item1)
            {
                if (run != null) yield return run;
                run = new List<Point> { segment.Item1 };
            }
            run.Add(segment.Item2);
        }
        if (run != null) yield return run;
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
        List<List<Point>> allGeometries;
        if (ClipOverflow)
        {
            allGeometries = ClipPolygon(geometry);
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
            var path = GetPathFromGeometry(geometryPart);
            if (path == null)
            {
                return;
            }

            SKPaint fillPaint = new()
            {
                Style = SKPaintStyle.Fill,
                StrokeCap = ConvertCap(style.Paint.LineCap),
                Color = new SKColor(style.Paint.FillColor.R, style.Paint.FillColor.G, style.Paint.FillColor.B, (byte)Clamp(style.Paint.FillColor.A * style.Paint.FillOpacity, 0, 255)),
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
        using var image = SKBitmap.Decode(imageStream);
        if (image == null)
        {
            return;
        }

        canvas.DrawBitmap(image, new SKRect(0, 0, width, height));
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
        OnBeforeFinishDrawing();

        return bitmap;
    }
}
