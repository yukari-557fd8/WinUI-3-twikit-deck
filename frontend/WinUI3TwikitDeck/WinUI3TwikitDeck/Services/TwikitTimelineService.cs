using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.Services;

public sealed class TwikitTimelineService(ITwikitClient client) : ITimelineService
{
    public Task<TimelinePage> LoadHomeTimelineAsync(string timelineType, string? cursor = null, CancellationToken cancellationToken = default)
        => client.GetHomeTimelineAsync(timelineType, cursor, cancellationToken);

    public Task<TimelinePage> SearchTweetsAsync(string query, string? cursor = null, CancellationToken cancellationToken = default)
        => client.SearchTweetsAsync(query, cursor, cancellationToken);

    public Task<TimelinePage> LoadNotificationsAsync(string notificationType, string? cursor = null, bool keepPagination = false, CancellationToken cancellationToken = default)
        => client.GetNotificationsAsync(notificationType, cursor, keepPagination, cancellationToken);

    public Task<IReadOnlyList<TwitterListModel>> LoadListsAsync(CancellationToken cancellationToken = default)
        => client.GetListsAsync(cancellationToken);

    public Task<TimelinePage> LoadListTimelineAsync(string listId, string? cursor = null, CancellationToken cancellationToken = default)
        => client.GetListTimelineAsync(listId, cursor, cancellationToken);

    public Task<TimelinePage> LoadUserTweetsAsync(string screenName, string tweetType, string? cursor = null, CancellationToken cancellationToken = default)
        => client.GetUserTweetsAsync(screenName, tweetType, cursor, cancellationToken);

    public Task<ConversationPage> LoadConversationAsync(string tweetId, string? cursor = null, CancellationToken cancellationToken = default)
        => client.GetConversationAsync(tweetId, cursor, cancellationToken);
}
