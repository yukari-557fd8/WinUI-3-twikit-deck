using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.Services;

/// <summary>Cookie 未設定時に UI を確認するためのダミーデータ。各タイムラインは 3 ページ分の続きを返す。</summary>
public sealed class MockTwikitClient : ITwikitClient
{
    private const int MaxPages = 3;

    private static long _idSeed = 1_800_000_000_000_000_000;

    private static string NextId() => Interlocked.Add(ref _idSeed, 1_000_000).ToString();

    private static string Now(int minutesAgo = 0)
        => DateTime.Now.AddMinutes(-minutesAgo).ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>"page:N" 形式のカーソルからページ番号（0 始まり）を取り出す。</summary>
    private static int PageOf(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor) || !cursor.StartsWith("page:", StringComparison.Ordinal))
        {
            return 0;
        }

        return int.TryParse(cursor["page:".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var page) ? page : 0;
    }

    private static TimelinePage Page(IReadOnlyList<object> items, int page)
        => new(items, page + 1 < MaxPages ? $"page:{page + 1}" : null);

    public async Task<TimelinePage> GetHomeTimelineAsync(string timelineType, string? cursor = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(300, cancellationToken);
        var page = PageOf(cursor);
        if (page > 0)
        {
            return Page(OlderCards("ホーム", page), page);
        }

        return Page(HomeTimelineType.Normalize(timelineType) == HomeTimelineType.Latest ? LatestCards() : ForYouCards(), page);
    }

    public async Task<TimelinePage> SearchTweetsAsync(string query, string? cursor = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(300, cancellationToken);
        var page = PageOf(cursor);
        if (page > 0)
        {
            return Page(OlderCards($"検索「{query}」", page), page);
        }

        return Page(
        [
            Card(
                userName: "検索",
                screenName: "@search",
                minutesAgo: 60,
                text: $"「{query}」の検索結果です。 #サンプル https://example.com/search",
                replies: 1,
                retweets: 0,
                likes: 3,
                views: 20),
            Card(
                userName: "画像つき",
                screenName: "@with_media",
                minutesAgo: 120,
                text: $"{query} に関する写真を 4 枚添付しました。",
                replies: 0,
                retweets: 2,
                likes: 10,
                views: 300,
                media:
                [
                    Image("https://picsum.photos/seed/deck1/600/400"),
                    Image("https://picsum.photos/seed/deck2/600/400"),
                    Image("https://picsum.photos/seed/deck3/600/400"),
                    Image("https://picsum.photos/seed/deck4/600/400"),
                ]),
        ], page);
    }

    public async Task<TimelinePage> GetNotificationsAsync(string notificationType, string? cursor = null, bool keepPagination = false, CancellationToken cancellationToken = default)
    {
        await Task.Delay(300, cancellationToken);
        var page = PageOf(cursor);
        if (page > 0)
        {
            return Page(OlderCards("通知", page), page);
        }

        var list = new List<object>
        {
            Card(
                userName: "返信してきた人",
                screenName: "@replier",
                minutesAgo: 3,
                text: "これはあなたへの返信です。",
                replies: 0,
                retweets: 0,
                likes: 1,
                views: 12,
                replyTo: "@you"),
        };

        if (NotificationType.Normalize(notificationType) == NotificationType.All)
        {
            list.Add(new NotificationCardModel
            {
                Id = NextId(),
                Type = "like",
                Text = "いいねした人さんがあなたのポストをいいねしました",
                ActorName = "いいねした人",
                ActorScreenName = "@liker",
                CreatedAt = Now(10),
                TargetTweetText = "いいねされた元のツイート本文です。",
            });
            list.Add(new NotificationCardModel
            {
                Id = NextId(),
                Type = "follow",
                Text = "フォロワーさんがあなたをフォローしました",
                ActorName = "フォロワー",
                ActorScreenName = "@follower",
                CreatedAt = Now(45),
            });
        }

        return Page(list, page);
    }

    public async Task<IReadOnlyList<TwitterListModel>> GetListsAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(200, cancellationToken);
        return
        [
            new TwitterListModel { Id = "1", Name = "ニュース", Description = "ニュースアカウント", Mode = "Public", MemberCount = 12, SubscriberCount = 3 },
            new TwitterListModel { Id = "2", Name = "友人", Description = "非公開リスト", Mode = "Private", MemberCount = 5, SubscriberCount = 0 },
        ];
    }

    public async Task<TimelinePage> GetListTimelineAsync(string listId, string? cursor = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(300, cancellationToken);
        var page = PageOf(cursor);
        if (page > 0)
        {
            return Page(OlderCards($"リスト {listId}", page), page);
        }

        return Page(
        [
            Card(
                userName: "リストメンバー",
                screenName: "@list_member",
                minutesAgo: 15,
                text: $"リスト {listId} のタイムラインのツイートです。",
                replies: 0,
                retweets: 1,
                likes: 2,
                views: 50),
        ], page);
    }

    public async Task<TimelinePage> GetUserTweetsAsync(string screenName, string tweetType, string? cursor = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(300, cancellationToken);
        var page = PageOf(cursor);
        var bare = screenName.TrimStart('@');
        var name = "@" + bare;
        if (page > 0)
        {
            return Page(OlderCards($"{name} ({tweetType})", page), page);
        }

        if (tweetType == "Media")
        {
            return Page(
            [
                Card(bare, name, 40, $"{name} の写真つきツイートです。", 1, 0, 9, 200,
                    media: [Image("https://picsum.photos/seed/user-photo/600/400")]),
                Card(bare, name, 300, $"{name} の動画つきツイートです。", 0, 1, 4, 150,
                    media:
                    [
                        new MediaItemModel
                        {
                            Type = "video",
                            Url = "https://test-videos.co.uk/vids/bigbuckbunny/mp4/h264/360/Big_Buck_Bunny_360_10s_1MB.mp4",
                            ThumbnailUrl = "https://picsum.photos/seed/user-video/600/340",
                        },
                    ]),
            ], page);
        }

        if (tweetType == "Replies")
        {
            return Page(
            [
                Card("相手の人", "@someone", 50, "元の投稿です（会話モジュールに含まれる他人のツイート）。", 1, 0, 2, 40),
                Card(bare, name, 45, "それはそうですね。", 0, 0, 1, 20, replyTo: "@someone"),
                Card(bare, name, 30, $"{name} のユーザータイムラインのツイートです。", 2, 0, 7, 120),
            ], page);
        }

        var pinned = Card(
            userName: bare,
            screenName: name,
            minutesAgo: 60 * 24 * 70,
            text: $"{name} のプロフィールに固定されたツイートです（70 日前）。",
            replies: 5,
            retweets: 3,
            likes: 25,
            views: 3800);
        pinned.IsPinned = true;

        return Page(
        [
            pinned,
            Card(bare, name, 30, $"{name} のユーザータイムラインのツイートです。", 2, 0, 7, 120),
            Card("リポスト元", "@reposted_user", 90, $"{name} がリポストした投稿です。", 3, 15, 40, 2100, retweetedBy: bare, retweeted: true),
        ], page);
    }

    /// <summary>続きページ用の古いツイート。</summary>
    private static IReadOnlyList<object> OlderCards(string source, int page)
    {
        var list = new List<object>();
        for (var i = 0; i < 5; i++)
        {
            list.Add(Card(
                userName: $"過去の投稿 {page}-{i + 1}",
                screenName: $"@older_{page}_{i + 1}",
                minutesAgo: page * 60 * 6 + i * 20,
                text: $"{source} の {page + 1} ページ目 ({i + 1}/5) のツイートです。",
                replies: i,
                retweets: page,
                likes: page * 3 + i,
                views: 100 * (page + 1)));
        }

        return list;
    }

    private static IReadOnlyList<object> ForYouCards() =>
    [
        Card(
            userName: "おすすめ",
            screenName: "@for_you",
            minutesAgo: 5,
            text: "おすすめタイムラインのツイートです。@for_you さんのリンク https://example.com/page と #ハッシュタグ を含みます。",
            verified: true,
            replies: 12,
            retweets: 4,
            likes: 28,
            views: 1240,
            media: [Image("https://picsum.photos/seed/foryou/800/450")]),
        Card(
            userName: "引用元",
            screenName: "@quoted",
            minutesAgo: 60 * 24,
            text: "引用つきのツイートです。",
            replies: 2,
            retweets: 1,
            likes: 9,
            views: 430,
            quote: new TweetQuoteModel
            {
                Id = NextId(),
                UserName = "元ツイート",
                UserScreenName = "@original",
                CreatedAt = Now(60 * 48),
                Text = "引用されているツイートです。",
                MediaItems = [Image("https://picsum.photos/seed/quote/600/400")],
            })
    ];

    private static IReadOnlyList<object> LatestCards() =>
    [
        Card(
            userName: "最新",
            screenName: "@latest",
            minutesAgo: 0,
            text: "最新タイムラインのツイートです。",
            replies: 0,
            retweets: 1,
            likes: 3,
            views: 86,
            liked: true,
            media:
            [
                Image("https://picsum.photos/seed/latest1/600/400"),
                Image("https://picsum.photos/seed/latest2/600/400"),
                Image("https://picsum.photos/seed/latest3/600/400"),
            ]),
        Card(
            userName: "リツイート先",
            screenName: "@retweeted",
            minutesAgo: 20,
            text: "リツイートされたツイートです。",
            replies: 3,
            retweets: 15,
            likes: 40,
            views: 2100,
            retweetedBy: "フォロー中",
            retweeted: true,
            media:
            [
                Image("https://picsum.photos/seed/rt1/600/400"),
                Image("https://picsum.photos/seed/rt2/600/400"),
            ])
    ];

    private static MediaItemModel Image(string url)
        => new() { Type = "image", Url = url, ThumbnailUrl = url };

    private static TweetCardModel Card(
        string userName,
        string screenName,
        int minutesAgo,
        string text,
        int replies,
        int retweets,
        int likes,
        int views,
        bool verified = false,
        bool liked = false,
        bool retweeted = false,
        string retweetedBy = "",
        string replyTo = "",
        TweetQuoteModel? quote = null,
        IReadOnlyList<MediaItemModel>? media = null)
    {
        var id = NextId();
        return new TweetCardModel
        {
            Id = id,
            TimelineEntryId = id,
            UserName = userName,
            UserScreenName = screenName,
            CreatedAt = Now(minutesAgo),
            Text = text,
            IsUserVerified = verified,
            ReplyCount = replies,
            RetweetCount = retweets,
            FavoriteCount = likes,
            ViewCount = views,
            IsLiked = liked,
            IsRetweeted = retweeted,
            IsRetweet = retweetedBy.Length > 0,
            RetweetedByName = retweetedBy,
            ReplyToDisplay = replyTo,
            QuotedTweet = quote,
            MediaItems = media ?? Array.Empty<MediaItemModel>(),
        };
    }
}
