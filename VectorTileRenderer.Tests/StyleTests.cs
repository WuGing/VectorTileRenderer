using NUnit.Framework;

namespace VectorTileRenderer.Tests;

[TestFixture]
public class StyleTests
{
    [TestCase("en-US")]
    [TestCase("de-DE")]
    [TestCase("fr-FR")]
    [NonParallelizable]
    public void ParseStyle_UsesInvariantNumericColors(string locale)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(locale);
            foreach (var color in new[] { "rgba(100,150,200,0.5)", "hsla(210.0,50%,50%,0.5)" })
            {
                var path = TestAssets.WriteTemporaryStyle("{\"layers\":[{\"id\":\"test\",\"type\":\"line\",\"paint\":{\"line-color\":\"" + color + "\"}}]}");
                var style = new Style(path);
                var brush = style.ParseStyle(style.Layers[0], 1, new Dictionary<string, object>());
                Assert.That(brush.Paint.LineColor.A, Is.EqualTo(127), color);
            }
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    [Test]
    public void Constructor_LoadsCheckedInStyleAndProducesStableHash()
    {
        var path = TestAssets.GetPath("styles", "basic-style.json");

        var first = new Style(path);
        var second = new Style(path);

        Assert.Multiple(() =>
        {
            Assert.That(first.Layers, Is.Not.Empty);
            Assert.That(first.Sources, Contains.Key("openmaptiles"));
            Assert.That(first.Hash, Has.Length.EqualTo(64));
            Assert.That(first.Hash, Is.EqualTo(second.Hash));
        });
    }

    [Test]
    public void ParseStyle_ResolvesFeatureTextAndScalesLineWidth()
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
                  "paint": { "line-color": "#112233", "line-width": 2 },
                  "layout": { "text-field": "Road {name}" }
                }
              ]
            }
            """);
        var style = new Style(path);

        var brush = style.ParseStyle(style.Layers[0], 1.5, new Dictionary<string, object>
        {
            ["name"] = "Main"
        });

        Assert.Multiple(() =>
        {
            Assert.That(brush.Text, Is.EqualTo("Road Main"));
            Assert.That(brush.Paint.LineWidth, Is.EqualTo(3));
            Assert.That(brush.Paint.LineColor.R, Is.EqualTo(0x11));
            Assert.That(brush.Paint.LineColor.G, Is.EqualTo(0x22));
            Assert.That(brush.Paint.LineColor.B, Is.EqualTo(0x33));
        });
    }

    [Test]
    public void ValidateLayer_AppliesZoomAndFeatureFilters()
    {
        var path = TestAssets.WriteTemporaryStyle("""
            {
              "layers": [
                {
                  "id": "major-roads",
                  "type": "line",
                  "minzoom": 5,
                  "maxzoom": 10,
                  "filter": ["==", "class", "major"]
                }
              ]
            }
            """);
        var style = new Style(path);
        var layer = style.Layers[0];

        Assert.Multiple(() =>
        {
            Assert.That(style.ValidateLayer(layer, 4, new Dictionary<string, object> { ["class"] = "major" }), Is.False);
            Assert.That(style.ValidateLayer(layer, 7, new Dictionary<string, object> { ["class"] = "minor" }), Is.False);
            Assert.That(style.ValidateLayer(layer, 7, new Dictionary<string, object> { ["class"] = "major" }), Is.True);
            Assert.That(style.ValidateLayer(layer, 11, new Dictionary<string, object> { ["class"] = "major" }), Is.False);
        });
    }
}
