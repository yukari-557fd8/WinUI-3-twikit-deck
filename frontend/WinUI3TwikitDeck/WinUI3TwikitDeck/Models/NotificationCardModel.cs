using System;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using WinUI3TwikitDeck.Helpers;

namespace WinUI3TwikitDeck.Models;

/// <summary>
/// いいね・リポスト・フォローなど、ツイートカードにならない通知 1 件。
/// 返信・メンション・引用は <see cref="TweetCardModel"/> として表示する。
/// 変更通知は表示倍率が変わったときにアバターを取り直させるためだけに使う。
/// </summary>
public sealed class NotificationCardModel : ObservableObject
{
    public string Id { get; init; } = string.Empty;

    /// <summary>like | retweet | follow | unknown</summary>
    public string Type { get; init; } = "unknown";

    public string Text { get; init; } = string.Empty;

    public string ActorName { get; init; } = string.Empty;

    public string ActorScreenName { get; init; } = string.Empty;

    public string ActorProfileImageUrl { get; init; } = string.Empty;

    public ImageSource? ActorProfileImage => ImageCache.GetAvatar(ActorProfileImageUrl);

    /// <summary>表示倍率が変わった。倍率に合わせてデコードし直したアバターをバインディングに取り直させる。</summary>
    public void NotifyDisplayScaleChanged() => OnPropertyChanged(nameof(ActorProfileImage));

    public string CreatedAt { get; init; } = string.Empty;

    public string CreatedAtAbsoluteDisplay => TimeDisplayHelper.FormatAbsoluteDisplay(CreatedAt);

    public string CreatedAtRelativeDisplay => TimeDisplayHelper.FormatRelativeDisplay(CreatedAt);

    public string TargetTweetText { get; init; } = string.Empty;

    public bool HasTargetTweet => !string.IsNullOrWhiteSpace(TargetTweetText);

    /// <summary>いいね・リポストされたツイートの ID。カードのクリックでその会話を開く（フォロー通知などは空）。</summary>
    public string TargetTweetId { get; init; } = string.Empty;

    public bool HasTargetTweetId => !string.IsNullOrEmpty(TargetTweetId);

    public ulong SortKey
    {
        get
        {
            var parsed = TimeDisplayHelper.TryParse(CreatedAt);
            return parsed is null ? 0UL : (ulong)parsed.Value.Ticks;
        }
    }

    private string Category => NormalizeCategory(Type, Text);

    public string IconGlyph => Category switch
    {
        "like" => "\uEB51",
        "retweet" => "\uE72A",
        "follow" => "\uE77B",
        _ => "\uEA8F",
    };

    /// <summary>UI スレッドのバインディングから呼ぶこと。</summary>
    public Brush IconForeground => Category switch
    {
        "like" => new SolidColorBrush(Colors.Red),
        "retweet" => new SolidColorBrush(Colors.LimeGreen),
        "follow" => new SolidColorBrush(Color("#1D9BF0")),
        _ => new SolidColorBrush(Colors.Gray),
    };

    private static Windows.UI.Color Color(string hex)
    {
        var value = Convert.ToUInt32(hex.TrimStart('#'), 16);
        return Windows.UI.Color.FromArgb(255, (byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    private static string NormalizeCategory(string? type, string? text)
    {
        var normalizedType = type?.Trim().ToLowerInvariant();
        if (normalizedType is "favorite" or "like")
        {
            return "like";
        }

        if (normalizedType is "retweet" or "repost")
        {
            return "retweet";
        }

        if (normalizedType is "follow")
        {
            return "follow";
        }

        var normalizedText = text?.ToLowerInvariant() ?? string.Empty;
        if (normalizedText.Contains("いいね") || normalizedText.Contains("お気に入り")
            || normalizedText.Contains("liked") || normalizedText.Contains("favorite"))
        {
            return "like";
        }

        if (normalizedText.Contains("リツイート") || normalizedText.Contains("リポスト")
            || normalizedText.Contains("retweeted") || normalizedText.Contains("reposted"))
        {
            return "retweet";
        }

        if (normalizedText.Contains("フォロー") || normalizedText.Contains("followed"))
        {
            return "follow";
        }

        return string.Empty;
    }

    public static NotificationCardModel FromJsonObject(JsonObject json)
    {
        var screenName = (json["actor_screen_name"]?.GetValue<string>() ?? string.Empty).Trim();
        return new NotificationCardModel
        {
            Id = json["id"]?.GetValue<string>() ?? string.Empty,
            Type = json["type"]?.GetValue<string>() ?? "unknown",
            Text = json["text"]?.GetValue<string>() ?? string.Empty,
            ActorName = json["actor_name"]?.GetValue<string>() ?? string.Empty,
            ActorScreenName = screenName.Length == 0 || screenName.StartsWith('@') ? screenName : "@" + screenName,
            ActorProfileImageUrl = json["actor_profile_image"]?.GetValue<string>() ?? string.Empty,
            CreatedAt = json["created_at"]?.GetValue<string>() ?? string.Empty,
            TargetTweetText = json["target_tweet_text"]?.GetValue<string>() ?? string.Empty,
            TargetTweetId = json["target_tweet_id"]?.GetValue<string>() ?? string.Empty,
        };
    }
}
