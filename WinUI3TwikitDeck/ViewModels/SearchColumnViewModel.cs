using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.Services;

namespace WinUI3TwikitDeck.ViewModels;

public sealed class SearchColumnViewModel : ColumnViewModelBase
{
    private readonly ITimelineService _timelineService;

    public SearchColumnViewModel(string query, ITimelineService timelineService)
        : base(new SearchColumn(query))
    {
        Query = query;
        _timelineService = timelineService;
    }

    public string Query { get; }

    protected override Task<IReadOnlyList<object>> LoadCoreAsync(CancellationToken cancellationToken)
        => _timelineService.SearchTweetsAsync(Query, cancellationToken);
}
