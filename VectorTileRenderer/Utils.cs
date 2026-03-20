using System;
using System.Text;

namespace WuGing.VectorTileRenderer;

static class Utils
{
    public static double ConvertRange(double oldValue, double oldMin, double oldMax, double newMin, double newMax, bool clamp = false)
    {
        double newRange;
        double newValue;
        double oldRange = oldMax - oldMin;
        if (oldRange == 0)
        {
            newValue = newMin;
        }
        else
        {
            newRange = newMax - newMin;
            newValue = ((oldValue - oldMin) * newRange / oldRange) + newMin;
        }

        if (clamp)
        {
            newValue = Math.Min(Math.Max(newValue, newMin), newMax);
        }

        return newValue;
    }

    public static string Sha256(string randomString)
    {
        var crypt = System.Security.Cryptography.SHA256.Create();
        var hash = new StringBuilder();
        byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(randomString));
        foreach (byte theByte in crypto)
        {
            hash.Append(theByte.ToString("x2"));
        }
        return hash.ToString();
    }
}