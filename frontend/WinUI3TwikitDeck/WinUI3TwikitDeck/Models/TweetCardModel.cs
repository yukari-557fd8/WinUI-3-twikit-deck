using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using WinUI3TwikitDeck.Helpers;

namespace WinUI3TwikitDeck.Models;

/// <summary>
/// カラムに表示するツイート 1 件。いいね・リツイートの状態とカウントは楽観的更新のため変更通知付き。
/// </summary>
public sealed class TweetCardModel : ObservableObject
{
    private bool _isLiked;
    private bool _isRetweeted;
    private bool _isPinned;
    private int _favoriteCount;
    private int _retweetCount;
    private int _replyCount;

    public string Id { get; init; } = string.Empty;

    /// <summary>タイムライン上のエントリ ID（リツイートはリツイート自身の ID）。重複判定に使う。</summary>
    public string TimelineEntryId { get; init; } = string.Empty;

    public string DedupKey => string.IsNullOrEmpty(TimelineEntryId) ? Id : TimelineEntryId;

    public ulong SortKey => TimeDisplayHelper.TryParseSnowflakeId(TimelineEntryId)
                            ?? TimeDisplayHelper.TryParseSnowflakeId(Id)
                            ?? 0UL;

    public string Text { get; init; } = string.Empty;

    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public IReadOnlyList<UrlEntityModel> Urls { get; init; } = Array.Empty<UrlEntityModel>();

    public string UserName { get; init; } = string.Empty;

    /// <summary>"@" 付きのスクリーンネーム。</summary>
    public string UserScreenName { get; init; } = string.Empty;

    /// <summary>"@" を除いたスクリーンネーム。</summary>
    public string UserScreenNameBare => UserScreenName.TrimStart('@');

    public string UserProfileImageUrl { get; init; } = string.Empty;

    /// <summary>UI スレッドのバインディングから呼ぶこと。</summary>
    public ImageSource? ProfileImage => ImageCache.GetAvatar(UserProfileImageUrl);

    public bool IsUserVerified { get; init; }

    public bool IsUserProtected { get; init; }

    public string CreatedAt { get; init; } = string.Empty;

    public string CreatedAtAbsoluteDisplay => TimeDisplayHelper.FormatAbsoluteDisplay(CreatedAt);

    public string CreatedAtRelativeDisplay => TimeDisplayHelper.FormatRelativeDisplay(CreatedAt);

    public string ReplyToDisplay { get; init; } = string.Empty;

    public bool HasReplyTo => !string.IsNullOrWhiteSpace(ReplyToDisplay);

    public bool IsRetweet { get; init; }

    public string RetweetedByName { get; init; } = string.Empty;

    public string RetweetHeaderText => IsRetweet
        ? $"{RetweetedByName}さんがリツイートしました"
        : string.Empty;

    /// <summary>
    /// プロフィールに固定されたツイート（ユーザーカラムの 1 ページ目）。
    /// 更新時に新着より上に留め、固定が外れたら <see cref="ColumnViewModelBase"/> が false にする。
    /// </summary>
    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }

    public int ReplyCount
    {
        get => _replyCount;
        set
        {
            if (SetProperty(ref _replyCount, value))
            {
                OnPropertyChanged(nameof(ReplyCountDisplay));
            }
        }
    }

    public int RetweetCount
    {
        get => _retweetCount;
        set
        {
            if (SetProperty(ref _retweetCount, value))
            {
                OnPropertyChanged(nameof(RetweetCountDisplay));
            }
        }
    }

    public int FavoriteCount
    {
        get => _favoriteCount;
        set
        {
            if (SetProperty(ref _favoriteCount, value))
            {
                OnPropertyChanged(nameof(FavoriteCountDisplay));
            }
        }
    }

    public int ViewCount { get; init; }

    public string ReplyCountDisplay => MetricDisplayHelper.Format(ReplyCount);

    public string RetweetCountDisplay => MetricDisplayHelper.Format(RetweetCount);

    public string FavoriteCountDisplay => MetricDisplayHelper.Format(FavoriteCount);

    public string ViewCountDisplay => MetricDisplayHelper.Format(ViewCount);

    public bool IsLiked
    {
        get => _isLiked;
        set => SetProperty(ref _isLiked, value);
    }

    public bool IsRetweeted
    {
        get => _isRetweeted;
        set
        {
            if (SetProperty(ref _isRetweeted, value))
            {
                OnPropertyChanged(nameof(RetweetMenuText));
            }
        }
    }

    public string RetweetMenuText => IsRetweeted ? "リツイートを取り消す" : "リツイート";

    public bool IsRetweetEnabled => !IsUserProtected;

    public TweetQuoteModel? QuotedTweet { get; init; }

    public bool HasQuotedTweet => QuotedTweet is not null;

    /// <summary>
    /// 表示倍率が変わった。倍率に合わせてデコードし直した画像（アバター・引用元）をバインディングに取り直させる。
    /// 本文のメディアは <see cref="Controls.MediaGrid"/> が自分で差し替える。
    /// </summary>
    public void NotifyDisplayScaleChanged()
    {
        OnPropertyChanged(nameof(ProfileImage));
        OnPropertyChanged(nameof(QuotedTweet));
    }

    public IReadOnlyList<MediaItemModel> MediaItems { get; init; } = Array.Empty<MediaItemModel>();

    public bool HasMedia => MediaItems.Count > 0;

    /// <summary>x.com 上の URL。</summary>
    public string TweetUrl => string.IsNullOrEmpty(Id)
        ? string.Empty
        : $"https://x.com/{(UserScreenNameBare.Length > 0 ? UserScreenNameBare : "i")}/status/{Id}";

    /// <summary>投稿画面の「返信先」「引用元」に出す 1 行要約。</summary>
    public string Summary
    {
        get
        {
            var body = Text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (body.Length > 80)
            {
                body = body[..80] + "…";
            }

            return $"{UserName} {UserScreenName}: {body}";
        }
    }

    public void ToggleLike()
    {
        IsLiked = !IsLiked;
        FavoriteCount = Math.Max(0, FavoriteCount + (IsLiked ? 1 : -1));
    }

    public void ToggleRetweet()
    {
        IsRetweeted = !IsRetweeted;
        RetweetCount = Math.Max(0, RetweetCount + (IsRetweeted ? 1 : -1));
    }

    public void NoteReplyPosted() => ReplyCount++;

    /// <summary>再取得した同じツイートの最新のカウント・状態を取り込む。</summary>
    public void ApplyRefresh(TweetCardModel fresh)
    {
        IsLiked = fresh.IsLiked;
        IsRetweeted = fresh.IsRetweeted;
        FavoriteCount = fresh.FavoriteCount;
        RetweetCount = fresh.RetweetCount;
        ReplyCount = fresh.ReplyCount;
    }

    private static string WithAt(string? screenName)
    {
        var name = (screenName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return string.Empty;
        }

        return name.StartsWith('@') ? name : "@" + name;
    }

    /// <summary>「返信先」に並べる人数。超えた分は「他 N 人」にまとめる。</summary>
    private const int MaxReplyToNames = 3;

    /// <summary>
    /// 空白区切りの返信先（直接の返信先と、本文の先頭から外した「@名前」）を「@a @b @c 他N人」の形にする。
    /// </summary>
    private static string ReplyToDisplayOf(string? screenNames)
    {
        var names = (screenNames ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (names.Length == 0)
        {
            return string.Empty;
        }

        var display = string.Join(" ", names.Take(MaxReplyToNames).Select(WithAt));
        return names.Length > MaxReplyToNames ? $"{display} 他{names.Length - MaxReplyToNames}人" : display;
    }

    private static int IntOf(JsonNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        try
        {
            return node.GetValue<int>();
        }
        catch (Exception)
        {
            return int.TryParse(node.ToString(), out var parsed) ? parsed : 0;
        }
    }

    private static List<MediaItemModel> MediaOf(JsonNode? node)
    {
        var list = new List<MediaItemModel>();
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (MediaItemModel.FromJsonObject(item as JsonObject) is { } media)
                {
                    list.Add(media);
                }
            }
        }

        return list;
    }

    private static List<UrlEntityModel> UrlsOf(JsonNode? node)
    {
        var list = new List<UrlEntityModel>();
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (UrlEntityModel.FromJsonObject(item as JsonObject) is { } url)
                {
                    list.Add(url);
                }
            }
        }

        return list;
    }

    /// <summary>
    /// TweetSerializer.TweetToDict / NotificationsService の出力 (JsonObject) から TweetCardModel を生成する。
    /// </summary>
    public static TweetCardModel FromJsonObject(JsonObject json)
    {
        TweetQuoteModel? quoteModel = null;
        if (json["quoted_tweet"] is JsonObject qObj)
        {
            var unavailable = qObj["is_unavailable"]?.GetValue<bool>() ?? false;
            quoteModel = new TweetQuoteModel
            {
                Id = qObj["id"]?.GetValue<string>() ?? string.Empty,
                Text = qObj["text"]?.GetValue<string>() ?? string.Empty,
                Urls = UrlsOf(qObj["urls"]),
                UserName = qObj["user_name"]?.GetValue<string>() ?? string.Empty,
                UserScreenName = WithAt(qObj["user_screen_name"]?.GetValue<string>()),
                UserProfileImageUrl = qObj["user_profile_image"]?.GetValue<string>() ?? string.Empty,
                IsUserVerified = qObj["user_verified"]?.GetValue<bool>() ?? false,
                IsUserProtected = qObj["user_protected"]?.GetValue<bool>() ?? false,
                CreatedAt = qObj["created_at"]?.GetValue<string>() ?? string.Empty,
                MediaItems = MediaOf(qObj["media_items"]),
                IsUnavailable = unavailable,
            };
        }

        var id = json["id"]?.GetValue<string>() ?? string.Empty;
        return new TweetCardModel
        {
            Id = id,
            TimelineEntryId = json["timeline_entry_id"]?.GetValue<string>() ?? id,
            Text = json["text"]?.GetValue<string>() ?? string.Empty,
            Urls = UrlsOf(json["urls"]),
            UserName = json["user_name"]?.GetValue<string>() ?? string.Empty,
            UserScreenName = WithAt(json["user_screen_name"]?.GetValue<string>()),
            UserProfileImageUrl = json["user_profile_image"]?.GetValue<string>() ?? string.Empty,
            IsUserVerified = json["user_verified"]?.GetValue<bool>() ?? false,
            IsUserProtected = json["user_protected"]?.GetValue<bool>() ?? false,
            CreatedAt = json["created_at"]?.GetValue<string>() ?? string.Empty,
            ReplyToDisplay = ReplyToDisplayOf(json["reply_to_screen_name"]?.GetValue<string>()),
            IsRetweet = json["is_retweet"]?.GetValue<bool>() ?? false,
            RetweetedByName = json["retweeted_by_name"]?.GetValue<string>() ?? string.Empty,
            ReplyCount = IntOf(json["reply_count"]),
            RetweetCount = IntOf(json["retweet_count"]),
            FavoriteCount = IntOf(json["favorite_count"]),
            ViewCount = IntOf(json["view_count"]),
            IsLiked = json["is_liked"]?.GetValue<bool>() ?? false,
            IsRetweeted = json["is_retweeted"]?.GetValue<bool>() ?? false,
            IsPinned = json["is_pinned"]?.GetValue<bool>() ?? false,
            QuotedTweet = quoteModel,
            MediaItems = MediaOf(json["media_items"]),
        };
    }
}

public sealed class TweetQuoteModel
{
    public string Id { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public IReadOnlyList<UrlEntityModel> Urls { get; init; } = Array.Empty<UrlEntityModel>();

    public string UserName { get; init; } = string.Empty;

    public string UserScreenName { get; init; } = string.Empty;

    public string UserProfileImageUrl { get; init; } = string.Empty;

    public ImageSource? ProfileImage => ImageCache.GetAvatar(UserProfileImageUrl);

    public bool IsUserVerified { get; init; }

    public bool IsUserProtected { get; init; }

    public string CreatedAt { get; init; } = string.Empty;

    public string CreatedAtAbsoluteDisplay => TimeDisplayHelper.FormatAbsoluteDisplay(CreatedAt);

    public string CreatedAtRelativeDisplay => TimeDisplayHelper.FormatRelativeDisplay(CreatedAt);

    public IReadOnlyList<MediaItemModel> MediaItems { get; init; } = Array.Empty<MediaItemModel>();

    public bool HasMedia => MediaItems.Count > 0;

    public bool IsUnavailable { get; init; }

    public bool IsAvailable => !IsUnavailable;
}
