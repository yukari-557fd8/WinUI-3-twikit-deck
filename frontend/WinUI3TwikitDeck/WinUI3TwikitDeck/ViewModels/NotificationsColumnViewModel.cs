using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinUI3TwikitDeck.Bridge;
using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.Services;

namespace WinUI3TwikitDeck.ViewModels;

/// <summary>通知（いいね・リポスト・フォロー・返信・引用）またはメンションのカラム。</summary>
public sealed class NotificationsColumnViewModel : ColumnViewModelBase
{
    private readonly ITimelineService _timelineService;

    public NotificationsColumnViewModel(string notificationType, ITimelineService timelineService)
        : base(new NotificationsColumn(Models.NotificationType.Normalize(notificationType)), timelineService)
    {
        NotificationType = Models.NotificationType.Normalize(notificationType);
        _timelineService = timelineService;
    }

    /// <summary>all | mentions</summary>
    public string NotificationType { get; }

    protected override Task<TimelinePage> LoadPageAsync(string? cursor, bool isRefresh, CancellationToken cancellationToken)
        => _timelineService.LoadNotificationsAsync(NotificationType, cursor, keepPagination: isRefresh, cancellationToken);

    public override DeckColumnDefinition ToDefinition()
    {
        var definition = base.ToDefinition();
        definition.Type = "notifications";
        definition.NotificationType = NotificationType;
        return definition;
    }
}
