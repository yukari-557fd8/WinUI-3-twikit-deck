using System;

namespace WinUI3TwikitDeck.Models;

public static class HomeTimelineType
{
    public const string ForYou = "for_you";
    public const string Latest = "latest";

    public static string Normalize(string? type)
        => string.Equals(type, Latest, StringComparison.OrdinalIgnoreCase) ? Latest : ForYou;
}
