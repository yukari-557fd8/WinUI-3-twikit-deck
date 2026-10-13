using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinUI3TwikitDeck.Bridge;
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

    public override string Header => "検索";

    public override string Subtitle => Query;

    protected override Task<TimelinePage> LoadPageAsync(string? cursor, bool isRefresh, CancellationToken cancellationToken)
        => _timelineService.SearchTweetsAsync(Query, cursor, cancellationToken);

    public override DeckColumnDefinition ToDefinition()
    {
        var definition = base.ToDefinition();
        definition.Type = "search";
        definition.Query = Query;
        return definition;
    }
}
