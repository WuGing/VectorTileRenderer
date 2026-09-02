namespace VectorTileRenderer.Tests;

internal static class TestAssets
{
    public static string GetPath(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (directory is not null)
        {
            var parts = new string[relativeParts.Length + 1];
            parts[0] = directory.FullName;
            Array.Copy(relativeParts, 0, parts, 1, relativeParts.Length);
            var candidate = Path.Combine(parts);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find test asset '{Path.Combine(relativeParts)}'.");
    }

    public static string WriteTemporaryStyle(string json)
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"style-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
