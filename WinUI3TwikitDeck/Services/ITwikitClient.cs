namespace WinUI3TwikitDeck.Services;

public interface ITwikitClient
{
    Task<IReadOnlyList<object>> GetHomeTimelineAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<object>> SearchTweetsAsync(string query, CancellationToken cancellationToken = default);
}
