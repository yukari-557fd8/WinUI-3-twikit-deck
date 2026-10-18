using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.Services;

/// <summary>
/// 読み取り系の API。<c>cursor</c> が null なら最新ページ、前回の <see cref="TimelinePage.NextCursor"/> を渡せば続き。
/// 失敗は例外で返す（カラム側でエラー表示する）。
/// </summary>
public interface ITwikitClient
{
    Task<TimelinePage> GetHomeTimelineAsync(string timelineType, string? cursor = null, CancellationToken cancellationToken = default);

    Task<TimelinePage> SearchTweetsAsync(string query, string? cursor = null, CancellationToken cancellationToken = default);

    /// <param name="notificationType">all | mentions</param>
    /// <param name="cursor">null で最新。続きは前回の <see cref="TimelinePage.NextCursor"/>。</param>
    /// <param name="keepPagination">最新取得のとき、続き用カーソルを捨てずに残す（一覧を保持したままの更新用）。</param>
    Task<TimelinePage> GetNotificationsAsync(string notificationType, string? cursor = null, bool keepPagination = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TwitterListModel>> GetListsAsync(CancellationToken cancellationToken = default);

    Task<TimelinePage> GetListTimelineAsync(string listId, string? cursor = null, CancellationToken cancellationToken = default);

    /// <param name="tweetType">Tweets（ツイート＋リポスト）/ Replies（ツイート＋返信）/ Media。</param>
    Task<TimelinePage> GetUserTweetsAsync(string screenName, string tweetType, string? cursor = null, CancellationToken cancellationToken = default);

    /// <summary>ツイートの会話。<paramref name="cursor"/> が null なら返信元・ツイート本体・返信の 1 ページ目、続きは返信だけ。</summary>
    Task<ConversationPage> GetConversationAsync(string tweetId, string? cursor = null, CancellationToken cancellationToken = default);
}
