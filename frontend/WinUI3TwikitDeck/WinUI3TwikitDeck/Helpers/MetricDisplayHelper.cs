using System;
using System.Globalization;

namespace WinUI3TwikitDeck.Helpers;

/// <summary>いいね数などの表示（WinUI-3-twikit より移植）。1 万以上は「1.2万」。</summary>
public static class MetricDisplayHelper
{
    private const int ManThreshold = 10_000;

    public static string Format(int count)
    {
        if (count < 0)
        {
            count = 0;
        }

        if (count < ManThreshold)
        {
            return count.ToString("#,0", CultureInfo.InvariantCulture);
        }

        var man = count / (double)ManThreshold;
        var rounded = Math.Round(man, 1, MidpointRounding.AwayFromZero);

        if (Math.Abs(rounded - Math.Truncate(rounded)) < 0.05)
        {
            return ((long)rounded).ToString(CultureInfo.InvariantCulture) + "万";
        }

        return rounded.ToString("0.0", CultureInfo.InvariantCulture) + "万";
    }
}
