using System.Globalization;
using System.Linq;
using HarfBuzzSharp;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace WuGing.VectorTileRenderer;

// Internal shaped font/script runs. Text is never shortened to obtain coverage.
internal sealed class TextLayout : IDisposable
{
    private sealed class Run
    {
        public string Text;
        public SKFont Font;
        public byte[] Glyphs;
        public SKPoint[] Points;
        public float[] Advances;
        public float X;
    }

    private readonly List<Run> runs = new();
    public float Width { get; private set; }
    internal string Text => string.Concat(runs.Select(r => r.Text));

    public TextLayout(string text, float size, Func<string, SKTypeface> resolve)
    {
        try
        {
            var elements = StringInfo.GetTextElementEnumerator(text);
            var groups = new List<(string Text, SKTypeface Font, Script Script)>();
            while (elements.MoveNext())
            {
                var element = elements.GetTextElement();
                var face = resolve(element);
                var codepoint = char.IsSurrogatePair(element, 0) ? char.ConvertToUtf32(element, 0) : element[0];
                var script = UnicodeFunctions.Default.GetScript(codepoint);
                if ((script == Script.Common || script == Script.Inherited) && groups.Count > 0)
                    script = groups[groups.Count - 1].Script;
                if (groups.Count > 0 && groups[groups.Count - 1].Font == face && groups[groups.Count - 1].Script == script)
                {
                    var previous = groups[groups.Count - 1];
                    groups[groups.Count - 1] = (previous.Text + element, face, script);
                }
                else groups.Add((element, face, script));
            }
            foreach (var group in groups)
            {
                var run = new Run { Text = group.Text, Font = new SKFont(group.Font, size) { Hinting = SKFontHinting.Normal }, X = Width };
                runs.Add(run);
                using var shaper = new SKShaper(group.Font);
                var shaped = shaper.Shape(group.Text, run.Font);
                var glyphs = shaped.Codepoints.Select(g => checked((ushort)g)).ToArray();
                run.Glyphs = new byte[glyphs.Length * 2];
                System.Buffer.BlockCopy(glyphs, 0, run.Glyphs, 0, run.Glyphs.Length);
                run.Points = shaped.Points;
                run.Advances = run.Font.GetGlyphWidths(glyphs);
                Width += shaped.Width;
            }
        }
        catch { Dispose(); throw; }
    }

    public PlacedText Place(float x, float y, SKPath path = null)
    {
        var result = new PlacedText();
        try
        {
            using var measure = path == null ? null : new SKPathMeasure(path, false);
            foreach (var run in runs)
            {
                SKTextBlob blob;
                if (measure == null)
                {
                    var positions = run.Points.Select(p => new SKPoint(p.X + run.X + x, p.Y + y)).ToArray();
                    blob = SKTextBlob.CreatePositioned(run.Glyphs, SKTextEncoding.GlyphId, run.Font, positions);
                }
                else
                {
                    var matrices = new SKRotationScaleMatrix[run.Points.Length];
                    for (var i = 0; i < matrices.Length; i++)
                    {
                        var half = run.Advances[i] / 2;
                        var distance = (measure.Length - Width) / 2 + x + run.X + run.Points[i].X + half;
                        if (!measure.GetPositionAndTangent(distance, out var point, out var tangent))
                            throw new InvalidOperationException("Shaped label exceeds its selected path.");
                        var vertical = y + run.Points[i].Y;
                        matrices[i] = new SKRotationScaleMatrix(tangent.X, tangent.Y,
                            point.X - tangent.X * half - tangent.Y * vertical,
                            point.Y - tangent.Y * half + tangent.X * vertical);
                    }
                    blob = SKTextBlob.CreateRotationScale(run.Glyphs, SKTextEncoding.GlyphId, run.Font, matrices);
                }
                result.Add(blob);
            }
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    public void Dispose()
    {
        foreach (var run in runs) run.Font.Dispose();
        runs.Clear();
    }
}
