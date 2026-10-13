using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinUI3TwikitDeck.Bridge;
using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.Services;

namespace WinUI3TwikitDeck.ViewModels;

/// <summary>リストのタイムラインカラム。</summary>
public sealed class ListColumnViewModel : ColumnViewModelBase
{
    private readonly ITimelineService _timelineService;

    public ListColumnViewModel(string listId, string listName, ITimelineService timelineService)
        : base(new ListColumn(listId, listName))
    {
        ListId = listId;
        ListName = listName;
        _timelineService = timelineService;
    }

    public string ListId { get; }

    public string ListName { get; }

    public override string Header => "リスト";

    public override string Subtitle => ListName;

    protected override Task<TimelinePage> LoadPageAsync(string? cursor, bool isRefresh, CancellationToken cancellationToken)
        => _timelineService.LoadListTimelineAsync(ListId, cursor, cancellationToken);

    public override DeckColumnDefinition ToDefinition()
    {
        var definition = base.ToDefinition();
        definition.Type = "list";
        definition.ListId = ListId;
        definition.ListName = ListName;
        return definition;
    }
}
