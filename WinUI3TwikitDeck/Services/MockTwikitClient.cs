namespace WinUI3TwikitDeck.Services;

public sealed class MockTwikitClient : ITwikitClient
{
    public Task<IReadOnlyList<object>> GetHomeTimelineAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<object>>(
        [
            "[Mock] Home tweet 1",
            "[Mock] Home tweet 2"
        ]);

    public Task<IReadOnlyList<object>> SearchTweetsAsync(string query, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<object>>(
        [
            $"[Mock] Search result for '{query}' #1",
            $"[Mock] Search result for '{query}' #2"
        ]);
}
