using System.Text.Json.Nodes;

namespace WinUI3TwikitDeck.Models;

/// <summary>本文中の t.co リンクと展開後 URL の対応。</summary>
public sealed class UrlEntityModel
{
    public string ShortUrl { get; init; } = string.Empty;

    public string ExpandedUrl { get; init; } = string.Empty;

    public string DisplayUrl { get; init; } = string.Empty;

    public static UrlEntityModel? FromJsonObject(JsonObject? json)
    {
        if (json is null)
        {
            return null;
        }

        var shortUrl = json["url"]?.GetValue<string>() ?? string.Empty;
        if (string.IsNullOrEmpty(shortUrl))
        {
            return null;
        }

        return new UrlEntityModel
        {
            ShortUrl = shortUrl,
            ExpandedUrl = json["expanded_url"]?.GetValue<string>() ?? shortUrl,
            DisplayUrl = json["display_url"]?.GetValue<string>() ?? string.Empty,
        };
    }
}
