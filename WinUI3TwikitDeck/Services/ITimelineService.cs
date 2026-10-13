namespace WinUI3TwikitDeck.Services;

public interface ITimelineService
{
    Task<IReadOnlyList<object>> LoadHomeTimelineAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<object>> SearchTweetsAsync(string query, CancellationToken cancellationToken = default);
}
