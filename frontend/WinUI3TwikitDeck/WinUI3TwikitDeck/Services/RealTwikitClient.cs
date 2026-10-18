using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WinUI3TwikitDeck.Bridge;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.Services;

/// <summary>
/// twikit-dotnet / Bridge 層を呼び出して実データを取得する ITwikitClient 実装。
/// Bridge が <c>error</c> を返した場合は例外にしてカラム側に伝える。
/// Bridge の <c>next_cursor</c>（<see cref="PageCursorStore{T}"/> のトークン）をそのまま次ページのカーソルにする。
/// </summary>
public sealed class RealTwikitClient : ITwikitClient
{
    private const int PageSize = 20;

    /// <summary>通知は Bridge 側が種別ごとにカーソルを持つため、「続きあり」を表す固定トークンを返す。</summary>
    private const string NotificationNextToken = "notifications:next";

    private static string? Cursor(string? cursor) => string.IsNullOrEmpty(cursor) ? null : cursor;

    private static TimelinePage PageOf(JsonObject response)
    {
        if (response["error"]?.GetValue<string>() is { Length: > 0 } error
            && (response["tweets"] is not JsonArray array || array.Count == 0))
        {
            throw new BridgeException(error);
        }

        var list = new List<object>();
        if (response["tweets"] is JsonArray tweetsArray)
        {
            foreach (var node in tweetsArray)
            {
                if (node is JsonObject tweetObj)
                {
                    list.Add(TweetCardModel.FromJsonObject(tweetObj));
                }
            }
        }

        var nextCursor = response["next_cursor"]?.GetValue<string>();
        return new TimelinePage(list, list.Count == 0 ? null : Cursor(nextCursor));
    }

    public async Task<TimelinePage> GetHomeTimelineAsync(string timelineType, string? cursor = null, CancellationToken cancellationToken = default)
    {
        var type = HomeTimelineType.Normalize(timelineType);
        var response = await TwikitSession.RunAsync(() => TimelineService.GetTimelineTweetsAsync(
            pages: 1,
            countPerPage: PageSize,
            timelineType: type,
            cursor: Cursor(cursor)), cancellationToken).ConfigureAwait(false);
        return PageOf(response);
    }

    public async Task<TimelinePage> SearchTweetsAsync(string query, string? cursor = null, CancellationToken cancellationToken = default)
    {
        var response = await TwikitSession.RunAsync(() => SearchService.SearchTweetsAsync(
            query: query,
            count: PageSize,
            product: "Latest",
            cursor: Cursor(cursor)), cancellationToken).ConfigureAwait(false);
        return PageOf(response);
    }

    public async Task<TimelinePage> GetNotificationsAsync(string notificationType, string? cursor = null, bool keepPagination = false, CancellationToken cancellationToken = default)
    {
        var type = NotificationType.Normalize(notificationType);
        var refresh = string.IsNullOrEmpty(cursor);
        var items = await TwikitSession.RunAsync(() => NotificationsService.GetNotificationsAsync(
            count: 40,
            refresh: refresh,
            type: type,
            keepCursor: refresh && keepPagination), cancellationToken).ConfigureAwait(false);

        var list = new List<object>();
        foreach (var node in items)
        {
            if (node is not JsonObject obj)
            {
                continue;
            }

            var kind = obj["kind"]?.GetValue<string>();
            if (kind == "tweet")
            {
                list.Add(TweetCardModel.FromJsonObject(obj));
            }
            else
            {
                list.Add(NotificationCardModel.FromJsonObject(obj));
            }
        }

        var hasMore = list.Count > 0 && NotificationsService.HasMore(type);
        return new TimelinePage(list, hasMore ? NotificationNextToken : null);
    }

    public async Task<IReadOnlyList<TwitterListModel>> GetListsAsync(CancellationToken cancellationToken = default)
    {
        var response = await TwikitSession.RunAsync(() => TimelineService.GetUserListsAsync(count: 100, cursor: null), cancellationToken).ConfigureAwait(false);
        if (response["error"]?.GetValue<string>() is { Length: > 0 } error)
        {
            throw new BridgeException(error);
        }

        var list = new List<TwitterListModel>();
        if (response["lists"] is JsonArray array)
        {
            foreach (var node in array)
            {
                if (node is JsonObject obj)
                {
                    list.Add(TwitterListModel.FromJsonObject(obj));
                }
            }
        }

        return list;
    }

    public async Task<TimelinePage> GetListTimelineAsync(string listId, string? cursor = null, CancellationToken cancellationToken = default)
    {
        var response = await TwikitSession.RunAsync(() => TimelineService.GetListTimelineAsync(listId, count: 30, cursor: Cursor(cursor)), cancellationToken).ConfigureAwait(false);
        return PageOf(response);
    }

    public async Task<TimelinePage> GetUserTweetsAsync(string screenName, string tweetType, string? cursor = null, CancellationToken cancellationToken = default)
    {
        var response = await TwikitSession.RunAsync(() => TimelineService.GetUserTweetsAsync(screenName, tweetType, count: 30, cursor: Cursor(cursor)), cancellationToken).ConfigureAwait(false);
        return PageOf(response);
    }

    /// <summary>会話は X のカーソルをそのまま続きに使う（<see cref="PageCursorStore{T}"/> は通さない）。</summary>
    public async Task<ConversationPage> GetConversationAsync(string tweetId, string? cursor = null, CancellationToken cancellationToken = default)
    {
        var response = await TwikitSession.RunAsync(() => ConversationService.GetConversationAsync(tweetId, Cursor(cursor)), cancellationToken).ConfigureAwait(false);
        if (response["error"]?.GetValue<string>() is { Length: > 0 } error)
        {
            throw new BridgeException(error);
        }

        var ancestors = new List<ConversationLink>();
        if (response["ancestors"] is JsonArray ancestorsArray)
        {
            foreach (var node in ancestorsArray)
            {
                if (node is JsonObject obj)
                {
                    ancestors.Add(LinkOf(obj));
                }
            }
        }

        var replies = new List<IReadOnlyList<TweetCardModel>>();
        if (response["replies"] is JsonArray threads)
        {
            foreach (var thread in threads)
            {
                var tweets = new List<TweetCardModel>();
                if (thread?["tweets"] is JsonArray tweetsArray)
                {
                    foreach (var node in tweetsArray)
                    {
                        if (node is JsonObject obj)
                        {
                            tweets.Add(TweetCardModel.FromJsonObject(obj));
                        }
                    }
                }

                if (tweets.Count > 0)
                {
                    replies.Add(tweets);
                }
            }
        }

        var focal = response["tweet"] is JsonObject focalObj ? LinkOf(focalObj) : null;
        return new ConversationPage(ancestors, focal, replies, Cursor(response["next_cursor"]?.GetValue<string>()));
    }

    private static ConversationLink LinkOf(JsonObject obj)
    {
        var id = obj["id"]?.GetValue<string>() ?? string.Empty;
        if (obj["is_unavailable"]?.GetValue<bool>() == true)
        {
            return new ConversationLink(id, null, null, obj["unavailable_text"]?.GetValue<string>());
        }

        return new ConversationLink(id, TweetCardModel.FromJsonObject(obj), obj["missing_parent_id"]?.GetValue<string>(), null);
    }
}
