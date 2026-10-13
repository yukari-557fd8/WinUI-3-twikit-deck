using System.Text.Json.Nodes;
using Microsoft.UI.Xaml.Media;
using WinUI3TwikitDeck.Helpers;

namespace WinUI3TwikitDeck.Models;

/// <summary>ツイートに添付されたメディア 1 件。</summary>
public sealed class MediaItemModel
{
    /// <summary>image | video | animated_gif</summary>
    public string Type { get; init; } = "image";

    /// <summary>画像なら画像 URL、動画/GIF なら mp4 の URL。</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>一覧に出すサムネイル。画像なら <see cref="Url"/> と同じ。</summary>
    public string ThumbnailUrl { get; init; } = string.Empty;

    public bool IsVideo => Type is "video" or "animated_gif";

    public bool IsGif => Type == "animated_gif";

    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailUrl);

    /// <summary>カラム内表示用（UI スレッドのバインディングから呼ぶこと）。</summary>
    public ImageSource? ThumbnailImage => ImageCache.GetOrCreate(TwitterImageUrls.ForListMedia(ThumbnailUrl), 640);

    /// <summary>拡大表示用の URL。</summary>
    public string PreviewUrl => IsVideo ? Url : TwitterImageUrls.ForPreview(Url) ?? Url;

    public static MediaItemModel? FromJsonObject(JsonObject? json)
    {
        if (json is null)
        {
            return null;
        }

        var type = json["type"]?.GetValue<string>() ?? "image";
        var url = json["url"]?.GetValue<string>() ?? string.Empty;
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        var thumbnail = json["thumbnail"]?.GetValue<string>();
        return new MediaItemModel
        {
            Type = type,
            Url = url,
            ThumbnailUrl = type is "video" or "animated_gif" ? thumbnail ?? string.Empty : thumbnail ?? url,
        };
    }
}
