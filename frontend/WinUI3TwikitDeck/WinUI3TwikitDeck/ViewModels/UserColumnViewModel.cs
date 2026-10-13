using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinUI3TwikitDeck.Bridge;
using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.Services;

namespace WinUI3TwikitDeck.ViewModels;

/// <summary>
/// 特定ユーザーのツイート一覧カラム。タブ（ツイート／返信／リポスト／メディア）で X の取得種別を切り替え、
/// 「オリジナルのみ」「返信のみ」「写真／動画」はクライアント側で絞り込む。
/// </summary>
public sealed class UserColumnViewModel : ColumnViewModelBase
{
    /// <summary>絞り込みでページが空になっても止まらないよう、1 回の取得で続けて読む最大ページ数。</summary>
    private const int MaxPagesPerFetch = 3;

    private readonly ITimelineService _timelineService;

    public UserColumnViewModel(string screenName, ITimelineService timelineService, string? tab = null, string? mediaKind = null)
        : base(new UserColumn(screenName))
    {
        ScreenName = screenName.Trim().TrimStart('@');
        _timelineService = timelineService;
        Tab = UserColumnTab.Normalize(tab);
        MediaKind = UserMediaKind.Normalize(mediaKind);
    }

    /// <summary>"@" 無し。</summary>
    public string ScreenName { get; }

    /// <summary><see cref="UserColumnTab"/> の値。</summary>
    public string Tab { get; private set; }

    /// <summary><see cref="UserMediaKind"/> の値。メディアタブのときだけ意味を持つ。</summary>
    public string MediaKind { get; private set; }

    public bool IsMediaTab => Tab == UserColumnTab.Media;

    public string MediaKindLabel => UserMediaKind.Label(MediaKind);

    public override string Header => "ユーザー";

    public override string Subtitle => "@" + ScreenName;

    public async Task SwitchTabAsync(string tab)
    {
        var normalized = UserColumnTab.Normalize(tab);
        if (string.Equals(Tab, normalized, StringComparison.Ordinal))
        {
            return;
        }

        Tab = normalized;
        OnPropertyChanged(nameof(Tab));
        OnPropertyChanged(nameof(IsMediaTab));
        RaiseSettingsChanged();
        Tweets.Clear();
        await LoadAsync();
    }

    public async Task SwitchMediaKindAsync(string kind)
    {
        var normalized = UserMediaKind.Normalize(kind);
        if (string.Equals(MediaKind, normalized, StringComparison.Ordinal))
        {
            return;
        }

        MediaKind = normalized;
        OnPropertyChanged(nameof(MediaKind));
        OnPropertyChanged(nameof(MediaKindLabel));
        RaiseSettingsChanged();
        if (IsMediaTab)
        {
            Tweets.Clear();
            await LoadAsync();
        }
    }

    protected override async Task<TimelinePage> LoadPageAsync(string? cursor, bool isRefresh, CancellationToken cancellationToken)
    {
        var tab = Tab;
        var mediaKind = MediaKind;
        var apiType = UserColumnTab.ApiTweetType(tab);

        var items = new List<object>();
        string? nextCursor = cursor;
        for (var pageIndex = 0; pageIndex < MaxPagesPerFetch; pageIndex++)
        {
            var page = await _timelineService.LoadUserTweetsAsync(ScreenName, apiType, nextCursor, cancellationToken);
            items.AddRange(page.Items.Where(item => Matches(item, tab, mediaKind)));
            nextCursor = page.NextCursor;

            // 絞り込み後に何か残ったか、続きが無いなら終わり。最新取得（isRefresh）は 1 ページで十分。
            if (items.Count > 0 || !page.HasMore || isRefresh)
            {
                break;
            }
        }

        return new TimelinePage(items, nextCursor);
    }

    private bool Matches(object item, string tab, string mediaKind)
    {
        if (item is not TweetCardModel tweet)
        {
            return false;
        }

        switch (tab)
        {
            case UserColumnTab.Retweets:
                return tweet.IsRetweet;

            case UserColumnTab.Replies:
                // Replies の応答は会話モジュールに相手のツイートも含むので、本人が書いた返信だけにする
                return !tweet.IsRetweet && tweet.HasReplyTo && IsOwnTweet(tweet);

            case UserColumnTab.Media:
                if (tweet.IsRetweet || !tweet.HasMedia)
                {
                    return false;
                }

                return mediaKind == UserMediaKind.Videos
                    ? tweet.MediaItems.Any(m => m.IsVideo)
                    : tweet.MediaItems.Any(m => !m.IsVideo);

            default:
                // オリジナルのみ: リポストと他人への返信を除く（自分のスレッドへの続きは残す）
                return !tweet.IsRetweet && (!tweet.HasReplyTo || IsSelfReply(tweet));
        }
    }

    private bool IsOwnTweet(TweetCardModel tweet)
        => string.Equals(tweet.UserScreenNameBare, ScreenName, StringComparison.OrdinalIgnoreCase);

    private bool IsSelfReply(TweetCardModel tweet)
        => string.Equals(tweet.ReplyToDisplay.TrimStart('@'), ScreenName, StringComparison.OrdinalIgnoreCase);

    public override DeckColumnDefinition ToDefinition()
    {
        var definition = base.ToDefinition();
        definition.Type = "user";
        definition.ScreenName = ScreenName;
        definition.UserTab = Tab;
        definition.MediaKind = MediaKind;
        return definition;
    }
}
