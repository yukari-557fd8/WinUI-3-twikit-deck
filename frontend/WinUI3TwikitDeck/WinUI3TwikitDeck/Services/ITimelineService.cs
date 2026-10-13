using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.Services;

/// <summary>カラムの ViewModel が使う読み取り API。<c>cursor</c> は null で最新、前回の NextCursor で続き。</summary>
public interface ITimelineService
{
    Task<TimelinePage> LoadHomeTimelineAsync(string timelineType, string? cursor = null, CancellationToken cancellationToken = default);

    Task<TimelinePage> SearchTweetsAsync(string query, string? cursor = null, CancellationToken cancellationToken = default);

    Task<TimelinePage> LoadNotificationsAsync(string notificationType, string? cursor = null, bool keepPagination = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TwitterListModel>> LoadListsAsync(CancellationToken cancellationToken = default);

    Task<TimelinePage> LoadListTimelineAsync(string listId, string? cursor = null, CancellationToken cancellationToken = default);

    /// <param name="tweetType">Tweets（ツイート＋リポスト）/ Replies（ツイート＋返信）/ Media。</param>
    Task<TimelinePage> LoadUserTweetsAsync(string screenName, string tweetType, string? cursor = null, CancellationToken cancellationToken = default);
}
