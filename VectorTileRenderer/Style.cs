using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using SkiaSharp;

namespace WuGing.VectorTileRenderer;

public class Brush
{
    public int ZIndex { get; set; } = 0;
    public Paint Paint { get; set; }
    public string TextField { get; set; }
    public string Text { get; set; }
    public string GlyphsDirectory { get; set; } = null;
    public Layer _layer { get; set; }
}

public enum SymbolPlacement
{
    Point,
    Line
}

public enum TextTransform
{
    None,
    Uppercase,
    Lowercase
}

public class Paint
{
    public Color BackgroundColor { get; set; }
    public string BackgroundPattern { get; set; }
    public double BackgroundOpacity { get; set; } = 1;

    public Color FillColor { get; set; }
    public string FillPattern { get; set; }
    public Point FillTranslate { get; set; } = new Point();
    public double FillOpacity { get; set; } = 1;

    public Color LineColor { get; set; }
    public string LinePattern { get; set; }
    public Point LineTranslate { get; set; } = new Point();
    public PenLineCap LineCap { get; set; } = PenLineCap.Flat;
    public double LineWidth { get; set; } = 1;
    public double LineOffset { get; set; } = 0;
    public double LineBlur { get; set; } = 0;
    public double[] LineDashArray { get; set; } = [];
    public double LineOpacity { get; set; } = 1;

    public SymbolPlacement SymbolPlacement { get; set; } = SymbolPlacement.Point;
    public double IconScale { get; set; } = 1;
    public string IconImage { get; set; }
    public double IconRotate { get; set; } = 0;
    public Point IconOffset { get; set; } = new Point();
    public double IconOpacity { get; set; } = 1;

    public Color TextColor { get; set; }
    public string[] TextFont { get; set; } = ["Open Sans Regular", "Arial Unicode MS Regular"];
    public double TextSize { get; set; } = 16;
    public double TextMaxWidth { get; set; } = 10;
    public TextAlignment TextJustify { get; set; } = TextAlignment.Center;
    public double TextRotate { get; set; } = 0;
    public Point TextOffset { get; set; } = new Point();
    public Color TextStrokeColor { get; set; }
    public double TextStrokeWidth { get; set; } = 0;
    public double TextStrokeBlur { get; set; } = 0;
    public bool TextOptional { get; set; } = false;
    public TextTransform TextTransform { get; set; } = TextTransform.None;
    public double TextOpacity { get; set; } = 1;

    public bool Visibility { get; set; } = true; // visibility
}

public class Layer
{
    public int Index { get; set; } = -1;
    public string ID { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public Source Source { get; set; } = null;
    public string SourceLayer { get; set; } = string.Empty;
    public Dictionary<string, object> Paint { get; set; } = [];
    public Dictionary<string, object> Layout { get; set; } = [];
    public object[] Filter { get; set; } = [];
    public double? MinZoom { get; set; } = null;
    public double? MaxZoom { get; set; } = null;
}

public class Source
{
    public string URL { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public Sources.ITileSource Provider { get; set; } = null;
    public double? MinZoom { get; set; } = null;
    public double? MaxZoom { get; set; } = null;
}

public class Style
{
    public readonly string Hash = string.Empty;
    public List<Layer> Layers = [];
    public Dictionary<string, Source> Sources = [];
    public Dictionary<string, object> Metadata = [];
    //double screenScale = 0.2;// = 0.3;
    //double emToPx = 16;

    private readonly ConcurrentDictionary<string, Brush[]> brushesCache = new();
    private readonly ConcurrentDictionary<string, Color> colorCache = new();
    private readonly Dictionary<int, bool> layerFeatureDependencyCache = [];

    public string FontDirectory { get; set; } = null;

    public Style(string path)
    {
        var json = System.IO.File.ReadAllText(path);
        var jObject = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        }).RootElement;

        if (jObject.TryGetProperty("metadata", out var metadataElement))
        {
            Metadata = PlainifyJson(metadataElement) as Dictionary<string, object>;
        }

        if (jObject.TryGetProperty("sources", out var sourcesElement))
        {
            foreach (var jSource in sourcesElement.EnumerateObject())
            {
                var source = new Source();

                var sourceDict = PlainifyJson(jSource.Value) as Dictionary<string, object>;

                source.Name = jSource.Name;

                if (sourceDict.TryGetValue("url", out var urlValue))
                {
                    source.URL = urlValue as string;
                }

                if (sourceDict.TryGetValue("type", out var typeValue))
                {
                    source.Type = typeValue as string;
                }

                if (sourceDict.TryGetValue("minzoom", out var minZoomValue))
                {
                    source.MinZoom = Convert.ToDouble(minZoomValue);
                }

                if (sourceDict.TryGetValue("maxzoom", out var maxZoomValue))
                {
                    source.MaxZoom = Convert.ToDouble(maxZoomValue);
                }

                Sources[jSource.Name] = source;
            }
        }

        int i = 0;
        if (jObject.TryGetProperty("layers", out var layersElement))
        {
            foreach (var jLayer in layersElement.EnumerateArray())
            {
                var layer = new Layer
                {
                    Index = i
                };

                var layerDict = PlainifyJson(jLayer) as Dictionary<string, object>;

                if (layerDict.TryGetValue("minzoom", out var minZoomValue))
                {
                    layer.MinZoom = Convert.ToDouble(minZoomValue);
                }

                if (layerDict.TryGetValue("maxzoom", out var maxZoomValue))
                {
                    layer.MaxZoom = Convert.ToDouble(maxZoomValue);
                }

                if (layerDict.TryGetValue("id", out var idValue))
                {
                    layer.ID = idValue as string;
                }

                if (layerDict.TryGetValue("type", out var typeValue))
                {
                    layer.Type = typeValue as string;
                }

                if (layerDict.TryGetValue("source", out var sourceValue))
                {
                    layer.SourceName = sourceValue as string;
                    if (Sources.TryGetValue(layer.SourceName, out var source))
                    {
                        layer.Source = source;
                    }
                }

                if (layerDict.TryGetValue("source-layer", out var sourceLayerValue))
                {
                    layer.SourceLayer = sourceLayerValue as string;
                }

                if (layerDict.TryGetValue("paint", out var paintValue))
                {
                    layer.Paint = paintValue as Dictionary<string, object>;
                }

                if (layerDict.TryGetValue("layout", out var layoutValue))
                {
                    layer.Layout = layoutValue as Dictionary<string, object>;
                }

                if (layerDict.TryGetValue("filter", out var filterValue))
                {
                    layer.Filter = filterValue as object[];
                }

                Layers.Add(layer);

                i++;
            }
        }

        Hash = Utils.Sha256(json);
    }

    public void SetSourceProvider(int index, Sources.ITileSource provider)
    {
        int i = 0;
        foreach (var pair in Sources)
        {
            if (index == i)
            {
                pair.Value.Provider = provider;
                return;
            }
            i++;
        }
    }

    public void SetSourceProvider(string name, Sources.ITileSource provider)
    {
        Sources[name].Provider = provider;
    }

    static object PlainifyJson(JsonElement token)
    {
        if (token.ValueKind == JsonValueKind.Object)
        {
            var dict = new Dictionary<string, object>();
            foreach (var pair in token.EnumerateObject())
            {
                dict[pair.Name] = PlainifyJson(pair.Value);
            }
            return dict;
        }
        else if (token.ValueKind == JsonValueKind.Array)
        {
            return token.EnumerateArray().Select(PlainifyJson).ToArray();
        }

        if (token.ValueKind == JsonValueKind.String)
        {
            return token.GetString();
        }

        if (token.ValueKind == JsonValueKind.Number)
        {
            if (token.TryGetInt64(out long int64Value))
            {
                return int64Value;
            }

            return token.GetDouble();
        }

        if (token.ValueKind == JsonValueKind.True || token.ValueKind == JsonValueKind.False)
        {
            return token.GetBoolean();
        }

        return null;
    }

    public Brush[] GetStyleByType(string type, double zoom, double scale = 1)
    {
        List<Brush> results = [];

        int i = 0;
        foreach (var layer in Layers)
        {
            if (layer.Type == type)
            {
                var attributes = new Dictionary<string, object>
                {
                    ["$type"] = "",
                    ["$id"] = "",
                    ["$zoom"] = zoom
                };

                results.Add(ParseStyle(layer, scale, attributes));
            }
            i++;
        }

        return results.ToArray();
    }

    public Color GetBackgroundColor(double zoom)
    {
        var brushes = GetStyleByType("background", zoom, 1);

        foreach (var brush in brushes)
        {
            var newColor = Color.FromArgb((byte)Math.Max(0, Math.Min(255, brush.Paint.BackgroundOpacity * brush.Paint.BackgroundColor.A)), brush.Paint.BackgroundColor.R, brush.Paint.BackgroundColor.G, brush.Paint.BackgroundColor.B);
            return newColor;
        }

        return Color.White;
    }

    public bool NeedsFeatureAttributes(Layer layer)
    {
        if (layerFeatureDependencyCache.TryGetValue(layer.Index, out var cachedResult))
        {
            return cachedResult;
        }

        var needs = (layer.Filter != null && layer.Filter.Length > 0)
            || TokenUsesFeatureAttributes(layer.Paint)
            || TokenUsesFeatureAttributes(layer.Layout);

        layerFeatureDependencyCache[layer.Index] = needs;
        return needs;
    }

    private static bool TokenUsesFeatureAttributes(object token)
    {
        if (token == null)
        {
            return false;
        }

        if (token is string s)
        {
            if (s.Contains('{') && s.IndexOf('}') > s.IndexOf('{'))
            {
                return true;
            }

            if (s.Length > 1 && s[0] == '$' && s != "$zoom" && s != "$id")
            {
                return true;
            }

            return false;
        }

        if (token is object[] arrayToken)
        {
            for (int i = 0; i < arrayToken.Length; i++)
            {
                if (TokenUsesFeatureAttributes(arrayToken[i]))
                {
                    return true;
                }
            }

            return false;
        }

        if (token is Dictionary<string, object> dictToken)
        {
            foreach (var pair in dictToken)
            {
                if (TokenUsesFeatureAttributes(pair.Value))
                {
                    return true;
                }
            }

            return false;
        }

        return false;
    }

    public Brush ParseStyle(Layer layer, double scale, Dictionary<string, object> attributes)
    {
        var paintData = layer.Paint;
        var layoutData = layer.Layout;
        var index = layer.Index;

        var brush = new Brush
        {
            ZIndex = index,
            _layer = layer,
            GlyphsDirectory = FontDirectory
        };

        var paint = new Paint();
        brush.Paint = paint;

        if (layer.ID == "country_label")
        {

        }

        if (paintData != null)
        {
            // --
            if (paintData.TryGetValue("fill-color", out var fillColorValue))
            {
                paint.FillColor = ParseColor(GetValue(fillColorValue, attributes));
            }

            if (paintData.TryGetValue("background-color", out var backgroundColorValue))
            {
                paint.BackgroundColor = ParseColor(GetValue(backgroundColorValue, attributes));
            }

            if (paintData.TryGetValue("text-color", out var textColorValue))
            {
                paint.TextColor = ParseColor(GetValue(textColorValue, attributes));
            }

            if (paintData.TryGetValue("line-color", out var lineColorValue))
            {
                paint.LineColor = ParseColor(GetValue(lineColorValue, attributes));
            }

            // --

            if (paintData.TryGetValue("line-pattern", out var linePatternValue))
            {
                paint.LinePattern = (string)GetValue(linePatternValue, attributes);
            }

            if (paintData.TryGetValue("background-pattern", out var backgroundPatternValue))
            {
                paint.BackgroundPattern = (string)GetValue(backgroundPatternValue, attributes);
            }

            if (paintData.TryGetValue("fill-pattern", out var fillPatternValue))
            {
                paint.FillPattern = (string)GetValue(fillPatternValue, attributes);
            }

            // --

            if (paintData.TryGetValue("text-opacity", out var textOpacityValue))
            {
                paint.TextOpacity = Convert.ToDouble(GetValue(textOpacityValue, attributes));
            }

            if (paintData.TryGetValue("icon-opacity", out var iconOpacityValue))
            {
                paint.IconOpacity = Convert.ToDouble(GetValue(iconOpacityValue, attributes));
            }

            if (paintData.TryGetValue("line-opacity", out var lineOpacityValue))
            {
                paint.LineOpacity = Convert.ToDouble(GetValue(lineOpacityValue, attributes));
            }

            if (paintData.TryGetValue("fill-opacity", out var fillOpacityValue))
            {
                paint.FillOpacity = Convert.ToDouble(GetValue(fillOpacityValue, attributes));
            }

            if (paintData.TryGetValue("background-opacity", out var backgroundOpacityValue))
            {
                paint.BackgroundOpacity = Convert.ToDouble(GetValue(backgroundOpacityValue, attributes));
            }

            // --

            if (paintData.TryGetValue("line-width", out var lineWidthValue))
            {
                paint.LineWidth = Convert.ToDouble(GetValue(lineWidthValue, attributes)) * scale; // * screenScale;
            }

            if (paintData.TryGetValue("line-offset", out var lineOffsetValue))
            {
                paint.LineOffset = Convert.ToDouble(GetValue(lineOffsetValue, attributes)) * scale;// * screenScale;
            }

            if (paintData.TryGetValue("line-dasharray", out var lineDashArrayValue))
            {
                var array = GetValue(lineDashArrayValue, attributes) as object[];
                paint.LineDashArray = array.Select(item => Convert.ToDouble(item) * scale).ToArray();
            }

            // --

            if (paintData.TryGetValue("text-halo-color", out var textHaloColorValue))
            {
                paint.TextStrokeColor = ParseColor(GetValue(textHaloColorValue, attributes));
            }

            if (paintData.TryGetValue("text-halo-width", out var textHaloWidthValue))
            {
                paint.TextStrokeWidth = Convert.ToDouble(GetValue(textHaloWidthValue, attributes)) * scale;
            }

            if (paintData.TryGetValue("text-halo-blur", out var textHaloBlurValue))
            {
                paint.TextStrokeBlur = Convert.ToDouble(GetValue(textHaloBlurValue, attributes)) * scale;
            }
        }

        if (layoutData != null)
        {
            if (layoutData.TryGetValue("line-cap", out var lineCapValue))
            {
                var value = (string)GetValue(lineCapValue, attributes);
                if (value == "butt")
                {
                    paint.LineCap = PenLineCap.Flat;
                }
                else if (value == "round")
                {
                    paint.LineCap = PenLineCap.Round;
                }
                else if (value == "square")
                {
                    paint.LineCap = PenLineCap.Square;
                }
            }

            if (layoutData.TryGetValue("visibility", out var visibilityValue))
            {
                paint.Visibility = ((string)GetValue(visibilityValue, attributes)) == "visible";
            }

            if (layoutData.TryGetValue("text-field", out var textFieldValue))
            {
                brush.TextField = (string)GetValue(textFieldValue, attributes);
                brush.Text = ResolveTextField(brush.TextField, attributes).Trim();
            }

            if (layoutData.TryGetValue("text-font", out var textFontValue))
            {
                paint.TextFont = [.. ((object[])GetValue(textFontValue, attributes)).Select(item => (string)item)];
            }

            if (layoutData.TryGetValue("text-size", out var textSizeValue))
            {
                paint.TextSize = Convert.ToDouble(GetValue(textSizeValue, attributes)) * scale;
            }

            if (layoutData.TryGetValue("text-max-width", out var textMaxWidthValue))
            {
                paint.TextMaxWidth = Convert.ToDouble(GetValue(textMaxWidthValue, attributes)) * scale;// * screenScale;
            }

            if (layoutData.TryGetValue("text-offset", out var textOffsetValue))
            {
                var value = (object[])GetValue(textOffsetValue, attributes);
                paint.TextOffset = new Point(Convert.ToDouble(value[0]) * scale, Convert.ToDouble(value[1]) * scale);
            }

            if (layoutData.TryGetValue("text-optional", out var textOptionalValue))
            {
                paint.TextOptional = (bool)GetValue(textOptionalValue, attributes);
            }

            if (layoutData.TryGetValue("text-transform", out var textTransformValue))
            {
                var value = (string)GetValue(textTransformValue, attributes);

                if (value == "none")
                {
                    paint.TextTransform = TextTransform.None;
                }
                else if (value == "uppercase")
                {
                    paint.TextTransform = TextTransform.Uppercase;
                }
                else if (value == "lowercase")
                {
                    paint.TextTransform = TextTransform.Lowercase;
                }
            }

            if (layoutData.TryGetValue("icon-size", out var iconSizeValue))
            {
                paint.IconScale = Convert.ToDouble(GetValue(iconSizeValue, attributes)) * scale;
            }

            if (layoutData.TryGetValue("icon-image", out var iconImageValue))
            {
                paint.IconImage = (string)GetValue(iconImageValue, attributes);
            }
        }

        return brush;
    }

    private static string ResolveTextField(string textField, Dictionary<string, object> attributes)
    {
        if (string.IsNullOrEmpty(textField) || attributes == null || attributes.Count == 0)
        {
            return textField ?? "";
        }

        if (textField.IndexOf('{') < 0)
        {
            return textField;
        }

        var builder = new StringBuilder(textField.Length + 8);

        for (int i = 0; i < textField.Length; i++)
        {
            var c = textField[i];

            if (c != '{')
            {
                builder.Append(c);
                continue;
            }

            var closeIndex = textField.IndexOf('}', i + 1);
            if (closeIndex < 0)
            {
                builder.Append(c);
                continue;
            }

            var key = textField.Substring(i + 1, closeIndex - i - 1);
            if (attributes.TryGetValue(key, out var value) && value != null)
            {
                builder.Append(value.ToString());
            }

            i = closeIndex;
        }

        return builder.ToString();
    }

    private Color ParseColor(object iColor)
    {
        if (iColor.GetType() == typeof(Color))
        {
            return (Color)iColor;
        }

        if (iColor.GetType() != typeof(string))
        {
            throw new NotImplementedException("Not implemented color format");
        }

        var colorString = (string)iColor;

        if (colorCache.TryGetValue(colorString, out var cachedColor))
        {
            return cachedColor;
        }

        var parsedColor = ParseColorString(colorString);
        colorCache[colorString] = parsedColor;
        return parsedColor;
    }

    private static Color ParseColorString(string colorString)
    {
        if (string.IsNullOrEmpty(colorString))
        {
            return Color.FromRgb(0, 0, 0);
        }

        if (colorString[0] == '#')
        {
            var parsedHex = SKColor.Parse(colorString);
            return Color.FromArgb(parsedHex.Alpha, parsedHex.Red, parsedHex.Green, parsedHex.Blue);
        }

        if (colorString.StartsWith("hsl("))
        {
            var segments = colorString.Replace('%', '\0').Split(',', '(', ')');
            double h = double.Parse(segments[1]);
            double s = double.Parse(segments[2]);
            double l = double.Parse(segments[3]);

            return HslToColor(h, s, l);
        }

        if (colorString.StartsWith("hsla("))
        {
            var segments = colorString.Replace('%', '\0').Split(',', '(', ')');
            double h = double.Parse(segments[1]);
            double s = double.Parse(segments[2]);
            double l = double.Parse(segments[3]);
            double a = double.Parse(segments[4]) * 255;

            var color = HslToColor(h, s, l);
            return Color.FromArgb((byte)a, color.R, color.G, color.B);
        }

        if (colorString.StartsWith("rgba("))
        {
            var segments = colorString.Replace('%', '\0').Split(',', '(', ')');
            double r = double.Parse(segments[1]);
            double g = double.Parse(segments[2]);
            double b = double.Parse(segments[3]);
            double a = double.Parse(segments[4]) * 255;

            return Color.FromArgb((byte)a, (byte)r, (byte)g, (byte)b);
        }

        if (colorString.StartsWith("rgb("))
        {
            var segments = colorString.Replace('%', '\0').Split(',', '(', ')');
            double r = double.Parse(segments[1]);
            double g = double.Parse(segments[2]);
            double b = double.Parse(segments[3]);

            return Color.FromRgb((byte)r, (byte)g, (byte)b);
        }

        try
        {
            var parsed = SKColor.Parse(colorString);
            return Color.FromArgb(parsed.Alpha, parsed.Red, parsed.Green, parsed.Blue);
        }
        catch (Exception)
        {
            throw new NotImplementedException("Not implemented color format: " + colorString);
        }
    }

    private static Color HslToColor(double h, double sPercent, double lPercent)
    {
        // CSS-style hsl() inputs use percentages for saturation and lightness.
        var s = Math.Max(0, Math.Min(100, sPercent)) / 100.0;
        var l = Math.Max(0, Math.Min(100, lPercent)) / 100.0;
        var normalizedHue = h % 360.0;
        if (normalizedHue < 0)
        {
            normalizedHue += 360.0;
        }

        var c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
        var x = c * (1.0 - Math.Abs(normalizedHue / 60.0 % 2.0 - 1.0));
        var m = l - c / 2.0;

        double rPrime = 0;
        double gPrime = 0;
        double bPrime = 0;

        if (normalizedHue < 60)
        {
            rPrime = c;
            gPrime = x;
        }
        else if (normalizedHue < 120)
        {
            rPrime = x;
            gPrime = c;
        }
        else if (normalizedHue < 180)
        {
            gPrime = c;
            bPrime = x;
        }
        else if (normalizedHue < 240)
        {
            gPrime = x;
            bPrime = c;
        }
        else if (normalizedHue < 300)
        {
            rPrime = x;
            bPrime = c;
        }
        else
        {
            rPrime = c;
            bPrime = x;
        }

        byte r = (byte)Math.Round((rPrime + m) * 255.0);
        byte g = (byte)Math.Round((gPrime + m) * 255.0);
        byte b = (byte)Math.Round((bPrime + m) * 255.0);

        return Color.FromRgb(r, g, b);
    }

    public bool ValidateLayer(Layer layer, double zoom, Dictionary<string, object> attributes)
    {
        if (layer.MinZoom != null)
        {
            if (zoom < layer.MinZoom.Value)
            {
                return false;
            }
        }

        if (layer.MaxZoom != null)
        {
            if (zoom > layer.MaxZoom.Value)
            {
                return false;
            }
        }

        if (attributes != null && layer.Filter.Length > 0)
        {
            if (!ValidateUsingFilter(layer.Filter, attributes))
            {
                return false;
            }
        }

        return true;
    }

    private Layer[] FindLayers(double zoom, string layerName, Dictionary<string, object> attributes)
    {
        List<Layer> result = [];

        foreach (var layer in Layers)
        {
            //if (attributes.ContainsKey("class"))
            //{
            //    if (id == "highway-trunk" && (string)attributes["class"] == "primary")
            //    {

            //    }
            //}

            if (layer.SourceLayer == layerName)
            {
                bool valid = true;

                if (layer.Filter.Length > 0)
                {
                    if (!ValidateUsingFilter(layer.Filter, attributes))
                    {
                        valid = false;
                    }
                }

                if (layer.MinZoom != null)
                {
                    if (zoom < layer.MinZoom.Value)
                    {
                        valid = false;
                    }
                }

                if (layer.MaxZoom != null)
                {
                    if (zoom > layer.MaxZoom.Value)
                    {
                        valid = false;
                    }
                }

                if (valid)
                {
                    //return layer;
                    result.Add(layer);
                }
            }
        }

        return result.ToArray();
    }

    private static bool ValidateUsingFilter(object[] filterArray, Dictionary<string, object> attributes)
    {
        if (filterArray.Length == 0)
        {
            return true;
        }
        var operation = filterArray[0] as string;
        bool result;

        if (operation == "all")
        {
            for (int i = 1; i < filterArray.Length; i++)
            {
                if (filterArray[i] is not object[] subFilter)
                {
                    continue;
                }
                if (!ValidateUsingFilter(subFilter, attributes))
                {
                    return false;
                }
            }
            return true;
        }
        else if (operation == "any")
        {
            for (int i = 1; i < filterArray.Length; i++)
            {
                if (filterArray[i] is not object[] subFilter)
                {
                    continue;
                }
                if (ValidateUsingFilter(subFilter, attributes))
                {
                    return true;
                }
            }
            return false;
        }
        else if (operation == "none")
        {
            result = false;
            for (int i = 1; i < filterArray.Length; i++)
            {
                if (filterArray[i] is not object[] subFilter)
                {
                    continue;
                }
                if (ValidateUsingFilter(subFilter, attributes))
                {
                    result = true;
                }
            }
            return !result;
        }

        switch (operation)
        {
            case "==":
            case "!=":
            case ">":
            case ">=":
            case "<":
            case "<=":

                var key = (string)filterArray[1];
                if (!attributes.TryGetValue(key, out var attributeValue))
                {
                    return operation != "==";
                }

                if (attributeValue is not IComparable)
                {
                    throw new NotImplementedException("Comparing colors probably");
                }

                var valueA = (IComparable)attributeValue;
                var valueB = GetValue(filterArray[2], attributes);

                if (IsNumber(valueA) && IsNumber(valueB))
                {
                    valueA = Convert.ToDouble(valueA);
                    valueB = Convert.ToDouble(valueB);
                }

                if (key is not null)
                {
                    if (key == "capital")
                    {

                    }
                }

                if (valueA.GetType() != valueB.GetType())
                {
                    return false;
                }

                var comparison = valueA.CompareTo(valueB);

                switch (operation)
                {
                    case "==":
                        return comparison == 0;
                    case "!=":
                        return comparison != 0;
                    case ">":
                        return comparison > 0;
                    case "<":
                        return comparison < 0;
                    case ">=":
                        return comparison >= 0;
                    case "<=":
                        return comparison <= 0;
                }

                break;
        }

        if (operation == "has")
        {
            return attributes.ContainsKey(filterArray[1] as string);
        }
        else if (operation == "!has")
        {
            return !attributes.ContainsKey(filterArray[1] as string);
        }

        if (operation == "in")
        {
            var key = filterArray[1] as string;
            if (!attributes.TryGetValue(key, out var value))
            {
                return false;
            }

            for (int i = 2; i < filterArray.Length; i++)
            {
                var item = filterArray[i];
                if (GetValue(item, attributes).Equals(value))
                {
                    return true;
                }
            }
            return false;
        }
        else if (operation == "!in")
        {
            var key = filterArray[1] as string;
            if (!attributes.TryGetValue(key, out var value))
            {
                return true;
            }

            for (int i = 2; i < filterArray.Length; i++)
            {
                var item = filterArray[i];
                if (GetValue(item, attributes).Equals(value))
                {
                    return false;
                }
            }
            return true;
        }

        return false;
    }

    static object GetValue(object token, Dictionary<string, object> attributes = null)
    {
        if (token is string && attributes != null)
        {
            string value = token as string;
            if (value.Length == 0)
            {
                return "";
            }
            if (value[0] == '$')
            {
                return GetValue(attributes[value]);
            }
        }

        if (token.GetType().IsArray)
        {
            var array = token as object[];
            //List<object> result = new List<object>();

            //foreach (object item in array)
            //{
            //    var obj = GetValue(item, attributes);
            //    result.Add(obj);
            //}

            //return result.ToArray();

            return array.Select(item => GetValue(item, attributes)).ToArray();
        }
        else if (token is Dictionary<string, object>)
        {
            var dict = token as Dictionary<string, object>;
            if (dict.TryGetValue("stops", out var stopsValue))
            {
                var stops = stopsValue as object[];
                // if it has stops, it's interpolation domain now :P
                //var pointStops = stops.Select(item => new Tuple<double, JToken>((item as JArray)[0].Value<double>(), (item as JArray)[1])).ToList();
                var pointStops = stops.Select(item => new Tuple<double, object>(Convert.ToDouble((item as object[])[0]), (item as object[])[1])).ToList();

                var zoom = (double)attributes["$zoom"];
                var minZoom = pointStops.First().Item1;
                var maxZoom = pointStops.Last().Item1;
                double power = 1;

                if (minZoom == 5 && maxZoom == 10)
                {

                }

                double zoomA = minZoom;
                double zoomB = maxZoom;
                int zoomAIndex = 0;
                int zoomBIndex = pointStops.Count - 1;

                // get min max zoom bounds from array
                if (zoom <= minZoom)
                {
                    //zoomA = minZoom;
                    //zoomB = pointStops[1].Item1;
                    return pointStops.First().Item2;
                }
                else if (zoom >= maxZoom)
                {
                    //zoomA = pointStops[pointStops.Count - 2].Item1;
                    //zoomB = maxZoom;
                    return pointStops.Last().Item2;
                }
                else
                {
                    // checking for consecutive values
                    for (int i = 1; i < pointStops.Count; i++)
                    {
                        var previousZoom = pointStops[i - 1].Item1;
                        var thisZoom = pointStops[i].Item1;

                        if (zoom >= previousZoom && zoom <= thisZoom)
                        {
                            zoomA = previousZoom;
                            zoomB = thisZoom;

                            zoomAIndex = i - 1;
                            zoomBIndex = i;
                            break;
                        }
                    }
                }

                if (dict.TryGetValue("base", out var baseValue))
                {
                    power = Convert.ToDouble(GetValue(baseValue, attributes));
                }

                //var referenceElement = (stops[0] as object[])[1];

                return InterpolateValues(pointStops[zoomAIndex].Item2, pointStops[zoomBIndex].Item2, zoomA, zoomB, zoom, power, false);
            }
        }


        //if (token is string)
        //{
        //    return token as string;
        //}
        //else if (token is bool)
        //{
        //    return (bool)token;
        //}
        //else if (token is float)
        //{
        //    return token as float;
        //}
        //else if (token.Type == JTokenType.Integer)
        //{
        //    return token.Value<int>();
        //}
        //else if (token.Type == JTokenType.None || token.Type == JTokenType.Null)
        //{
        //    return null;
        //}


        return token;
    }

    private static bool IsNumber(object value)
    {
        return value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }

    private static object InterpolateValues(object startValue, object endValue, double zoomA, double zoomB, double zoom, double power, bool clamp = false)
    {
        if (startValue is string)
        {
            // TODO implement color mappings
            //var minValue = ParseColor(startValue.Value<string>());
            //var maxValue = ParseColor(endValue.Value<string>());


            //var newR = convertRange(zoom, zoomA, zoomB, minValue.ScR, maxValue.ScR, power, false);
            //var newG = convertRange(zoom, zoomA, zoomB, minValue.ScG, maxValue.ScG, power, false);
            //var newB = convertRange(zoom, zoomA, zoomB, minValue.ScB, maxValue.ScB, power, false);
            //var newA = convertRange(zoom, zoomA, zoomB, minValue.ScA, maxValue.ScA, power, false);

            //return Color.FromScRgb((float)newA, (float)newR, (float)newG, (float)newB);

            var minValue = startValue as string;
            var maxValue = endValue as string;

            if (Math.Abs(zoomA - zoom) <= Math.Abs(zoomB - zoom))
            {
                return minValue;
            }
            else
            {
                return maxValue;
            }

        }
        else if (startValue.GetType().IsArray)
        {
            List<object> result = [];
            var startArray = startValue as object[];
            var endArray = endValue as object[];

            for (int i = 0; i < startArray.Length; i++)
            {
                var minValue = startArray[i];
                var maxValue = endArray[i];

                var value = InterpolateValues(minValue, maxValue, zoomA, zoomB, zoom, power, clamp);

                result.Add(value);
            }

            return result.ToArray();
        }
        else if (IsNumber(startValue))
        {
            var minValue = Convert.ToDouble(startValue);
            var maxValue = Convert.ToDouble(endValue);

            return InterpolateRange(zoom, zoomA, zoomB, minValue, maxValue, power, clamp);
        }
        else
        {
            throw new NotImplementedException("Unimplemented interpolation");
        }
    }

    private static double InterpolateRange(double oldValue, double oldMin, double oldMax, double newMin, double newMax, double power, bool clamp = false)
    {
        double difference = oldMax - oldMin;
        double progress = oldValue - oldMin;

        double normalized;
        if (difference == 0)
        {
            normalized = 0;
        }
        else if (power == 1)
        {
            normalized = progress / difference;
        }
        else
        {
            normalized = (Math.Pow(power, progress) - 1f) / (Math.Pow(power, difference) - 1f);
        }

        var result = (normalized * (newMax - newMin)) + newMin;

        return result;
    }
}