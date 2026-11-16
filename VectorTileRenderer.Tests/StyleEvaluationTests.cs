using System.Globalization;
using System.Reflection;

namespace VectorTileRenderer.Tests;

public class StyleEvaluationTests
{
    private static readonly Func<object, Dictionary<string, object>, object> resolve = typeof(Style)
        .GetMethod("GetValue", BindingFlags.Static | BindingFlags.NonPublic)
        .CreateDelegate<Func<object, Dictionary<string, object>, object>>();

    [TestCase("==")]
    [TestCase("!=")]
    [TestCase(">")]
    [TestCase(">=")]
    [TestCase("<")]
    [TestCase("<=")]
    public void ValidateLayer_PreservesNumericCoercionAndMissingAttributeResults(string operation)
    {
        var style = EmptyStyle();
        var layer = new Layer();
        var attributes = new Dictionary<string, object>();
        object[] values = [(sbyte)-1, (byte)7, (short)-3, (ushort)7, 7, 7U, long.MinValue, ulong.MaxValue,
            7.5f, float.NaN, 7.5d, double.NaN, double.PositiveInfinity, double.NegativeInfinity, decimal.MaxValue];
        foreach (var left in values)
        foreach (var right in values)
        {
            attributes["rank"] = left;
            layer.Filter = [operation, "rank", right];
            var comparison = Convert.ToDouble(left).CompareTo(Convert.ToDouble(right));
            var expected = operation switch
            {
                "==" => comparison == 0, "!=" => comparison != 0, ">" => comparison > 0,
                ">=" => comparison >= 0, "<" => comparison < 0, _ => comparison <= 0
            };
            Assert.That(style.ValidateLayer(layer, 14, attributes), Is.EqualTo(expected), $"{left}/{right}");
        }
        attributes.Clear();
        layer.Filter = [operation, "rank", null];
        Assert.That(style.ValidateLayer(layer, 14, attributes), Is.EqualTo(operation != "=="));
    }

    [TestCase("en-US")]
    [TestCase("de-DE")]
    [TestCase("tr-TR")]
    [NonParallelizable]
    public void ValidateLayer_StringComparisonsFollowCurrentCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var style = EmptyStyle();
            foreach (var pair in new[] { ("a", "a"), ("straße", "strasse"), ("I", "ı"), ("a\0", "a") })
            {
                var expected = ((IComparable)pair.Item1).CompareTo(pair.Item2) == 0;
                var layer = new Layer { Filter = ["==", "name", pair.Item2] };
                Assert.That(style.ValidateLayer(layer, 14, new() { ["name"] = pair.Item1 }), Is.EqualTo(expected));
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Test]
    [NonParallelizable]
    public void ValidateLayer_NumericComparisonDoesNotBoxTemporaryDoubles()
    {
        var style = EmptyStyle();
        var layer = new Layer { Filter = ["==", "rank", 7d] };
        var attributes = new Dictionary<string, object> { ["rank"] = 7L };
        for (var i = 0; i < 100; i++) style.ValidateLayer(layer, 14, attributes);
        var accepted = true;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++) accepted &= style.ValidateLayer(layer, 14, attributes);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(accepted, Is.True);
        Assert.That(allocated, Is.LessThan(4096));
    }

    [Test]
    public void ValidateLayer_PreservesMembershipTypesAndCompositeEvaluationOrder()
    {
        var style = EmptyStyle();
        var attributes = new Dictionary<string, object> { ["rank"] = 7, ["class"] = "major" };
        var layer = new Layer { Filter = ["in", "rank", 7L] };
        Assert.That(style.ValidateLayer(layer, 14, attributes), Is.False);
        layer.Filter = ["!in", "rank", 7L];
        Assert.That(style.ValidateLayer(layer, 14, attributes), Is.True);
        layer.Filter = ["!=", "rank", "7"];
        Assert.That(style.ValidateLayer(layer, 14, attributes), Is.False);
        layer.Filter = ["all", new object[] { "==", "class", "minor" }, new object[] { "==", "rank", null }];
        Assert.That(style.ValidateLayer(layer, 14, attributes), Is.False);
        layer.Filter = ["any", new object[] { "has", "class" }, new object[] { "==", "rank", null }];
        Assert.That(style.ValidateLayer(layer, 14, attributes), Is.True);
        layer.Filter = ["none", new object[] { "has", "class" }, new object[] { "==", "rank", null }];
        Assert.Throws<NullReferenceException>(() => style.ValidateLayer(layer, 14, attributes));
        object[] predicate = ["==", "class", "major"];
        layer.Filter = ["all", 42, predicate];
        Assert.That(style.ValidateLayer(layer, 14, attributes), Is.True);
        predicate[2] = "minor";
        Assert.That(style.ValidateLayer(layer, 14, attributes), Is.False);
    }

    [Test]
    public void GetValue_PreservesAttributeReferenceAndArrayResolution()
    {
        var attributes = new Dictionary<string, object> { ["$number"] = 7d, ["$nested"] = "$number", ["$array"] = new object[] { "$number" } };
        Assert.That(resolve("$nested", attributes), Is.EqualTo("$number"));
        Assert.That(resolve("$array", attributes), Is.EqualTo(new object[] { "$number" }));
        object[] input = ["$number", "literal"];
        Assert.That(resolve(input, attributes), Is.EqualTo(new object[] { 7d, "literal" }));
        Assert.That(input[0], Is.EqualTo("$number"));
        attributes["$number"] = 9d;
        Assert.That(resolve(input, attributes), Is.EqualTo(new object[] { 9d, "literal" }));
        Assert.Throws<NullReferenceException>(() => resolve(null, attributes));
        Assert.Throws<NullReferenceException>(() => resolve(new int[] { 1 }, attributes));
    }

    [Test]
    public void GetValue_StopInterpolationObservesMutationsAndEagerConversionErrors()
    {
        object[] first = [5d, 2d];
        object[] last = [10d, 10d];
        var function = new Dictionary<string, object> { ["stops"] = new object[] { first, last } };
        var attributes = new Dictionary<string, object> { ["$zoom"] = 4d };
        Assert.That(resolve(function, attributes), Is.EqualTo(2d));
        attributes["$zoom"] = 7.5d;
        Assert.That(resolve(function, attributes), Is.EqualTo(6d));
        function["base"] = 2d;
        Assert.That(resolve(function, attributes), Is.EqualTo(2 + (Math.Pow(2, 2.5) - 1) / (Math.Pow(2, 5) - 1) * 8));
        last[1] = 18d;
        attributes["$zoom"] = 11d;
        Assert.That(resolve(function, attributes), Is.EqualTo(18d));
        last[0] = "bad-number";
        attributes["$zoom"] = 4d;
        Assert.Throws<FormatException>(() => resolve(function, attributes));
        function["stops"] = Array.Empty<object>();
        Assert.Throws<InvalidOperationException>(() => resolve(function, attributes));
        attributes.Clear();
        Assert.Throws<KeyNotFoundException>(() => resolve(function, attributes));
        function["stops"] = null;
        Assert.That(Assert.Throws<ArgumentNullException>(() => resolve(function, attributes)).ParamName, Is.EqualTo("source"));
    }

    private static Style EmptyStyle() => new(TestAssets.WriteTemporaryStyle("""{"layers":[]}"""));
}
