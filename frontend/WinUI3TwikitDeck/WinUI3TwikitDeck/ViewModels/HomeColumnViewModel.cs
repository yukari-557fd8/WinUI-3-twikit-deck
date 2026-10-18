using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinUI3TwikitDeck.Bridge;
using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.Services;

namespace WinUI3TwikitDeck.ViewModels;

public sealed class HomeColumnViewModel : ColumnViewModelBase
{
    private readonly ITimelineService _timelineService;

    public HomeColumnViewModel(ITimelineService timelineService, string? timelineType = null)
        : base(new HomeColumn(), timelineService)
    {
        _timelineService = timelineService;
        TimelineType = HomeTimelineType.Normalize(timelineType);
    }

    public string TimelineType { get; private set; }

    public async Task SwitchTimelineAsync(string timelineType)
    {
        var normalized = HomeTimelineType.Normalize(timelineType);
        if (string.Equals(TimelineType, normalized, StringComparison.Ordinal))
        {
            return;
        }

        TimelineType = normalized;
        RaiseSettingsChanged();
        Tweets.Clear();
        await LoadAsync();
    }

    protected override Task<TimelinePage> LoadPageAsync(string? cursor, bool isRefresh, CancellationToken cancellationToken)
        => _timelineService.LoadHomeTimelineAsync(TimelineType, cursor, cancellationToken);

    public override DeckColumnDefinition ToDefinition()
    {
        var definition = base.ToDefinition();
        definition.Type = "home";
        definition.TimelineType = TimelineType;
        return definition;
    }
}
