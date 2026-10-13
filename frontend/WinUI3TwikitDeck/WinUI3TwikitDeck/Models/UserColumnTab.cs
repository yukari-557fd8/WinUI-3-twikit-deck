using System;

namespace WinUI3TwikitDeck.Models;

/// <summary>ユーザーカラムのタブ。保存ファイルの値にもなる。</summary>
public static class UserColumnTab
{
    /// <summary>オリジナルのツイートのみ（リポストと他人への返信を除く）。</summary>
    public const string Tweets = "tweets";

    /// <summary>そのユーザーが書いた返信のみ。</summary>
    public const string Replies = "replies";

    /// <summary>そのユーザーのリポストのみ。</summary>
    public const string Retweets = "retweets";

    /// <summary>メディア付きツイート（写真 / 動画は <see cref="UserMediaKind"/> で切り替え）。</summary>
    public const string Media = "media";

    public static string Normalize(string? tab) => tab?.ToLowerInvariant() switch
    {
        Replies => Replies,
        Retweets => Retweets,
        Media => Media,
        _ => Tweets,
    };

    /// <summary>タブに対応する X の UserTweets 種別（twikit の tweetType）。</summary>
    public static string ApiTweetType(string tab) => Normalize(tab) switch
    {
        Replies => "Replies",
        Media => "Media",
        _ => "Tweets",
    };

    public static string Label(string tab) => Normalize(tab) switch
    {
        Replies => "返信",
        Retweets => "リポスト",
        Media => "メディア",
        _ => "ツイート",
    };
}

/// <summary>メディアタブの絞り込み。</summary>
public static class UserMediaKind
{
    public const string Photos = "photos";

    public const string Videos = "videos";

    public static string Normalize(string? kind)
        => string.Equals(kind, Videos, StringComparison.OrdinalIgnoreCase) ? Videos : Photos;

    public static string Label(string kind) => Normalize(kind) == Videos ? "動画" : "写真";
}
