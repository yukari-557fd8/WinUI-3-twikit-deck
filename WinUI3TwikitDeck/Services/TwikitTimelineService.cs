namespace WinUI3TwikitDeck.Services;

public sealed class TwikitTimelineService(ITwikitClient client) : ITimelineService
{
    public Task<IReadOnlyList<object>> LoadHomeTimelineAsync(CancellationToken cancellationToken = default)
        => client.GetHomeTimelineAsync(cancellationToken);

    public Task<IReadOnlyList<object>> SearchTweetsAsync(string query, CancellationToken cancellationToken = default)
        => client.SearchTweetsAsync(query, cancellationToken);
}
