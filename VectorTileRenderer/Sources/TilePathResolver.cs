using System.Text;

namespace WuGing.VectorTileRenderer.Sources;

internal static class TilePathResolver
{
    public static string Resolve(string templatePath, int x, int y, int z)
    {
        if (string.IsNullOrEmpty(templatePath) || templatePath.IndexOf('{') < 0)
        {
            return templatePath;
        }

        var builder = new StringBuilder(templatePath.Length + 8);
        string xString = null;
        string yString = null;
        string zString = null;

        for (int i = 0; i < templatePath.Length; i++)
        {
            var c = templatePath[i];
            if (c != '{')
            {
                builder.Append(c);
                continue;
            }

            var closeIndex = templatePath.IndexOf('}', i + 1);
            if (closeIndex < 0)
            {
                builder.Append(c);
                continue;
            }

            var tokenLength = closeIndex - i - 1;
            if (tokenLength == 1)
            {
                switch (templatePath[i + 1])
                {
                    case 'x':
                        xString ??= x.ToString();
                        builder.Append(xString);
                        i = closeIndex;
                        continue;
                    case 'y':
                        yString ??= y.ToString();
                        builder.Append(yString);
                        i = closeIndex;
                        continue;
                    case 'z':
                        zString ??= z.ToString();
                        builder.Append(zString);
                        i = closeIndex;
                        continue;
                }
            }

            builder.Append(templatePath, i, closeIndex - i + 1);
            i = closeIndex;
        }

        return builder.ToString();
    }
}
