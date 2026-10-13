using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.Services;

namespace WinUI3TwikitDeck.ViewModels;

public sealed class HomeColumnViewModel(ITimelineService timelineService) : ColumnViewModelBase(new HomeColumn())
{
    protected override Task<IReadOnlyList<object>> LoadCoreAsync(CancellationToken cancellationToken)
        => timelineService.LoadHomeTimelineAsync(cancellationToken);
}
