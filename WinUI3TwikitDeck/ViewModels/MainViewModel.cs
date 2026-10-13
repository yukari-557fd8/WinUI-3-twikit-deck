using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUI3TwikitDeck.Services;

namespace WinUI3TwikitDeck.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ITimelineService _timelineService;
    private int _searchCounter = 1;

    public MainViewModel(ITimelineService timelineService)
    {
        _timelineService = timelineService;
        AddHomeColumnCommand = new AsyncRelayCommand(AddHomeColumnAsync);
        AddSearchColumnCommand = new AsyncRelayCommand(AddSearchColumnAsync);
        _ = AddHomeColumnAsync();
    }

    public ObservableCollection<ColumnViewModelBase> Columns { get; } = [];

    public IAsyncRelayCommand AddHomeColumnCommand { get; }

    public IAsyncRelayCommand AddSearchColumnCommand { get; }

    public async Task AddHomeColumnAsync()
    {
        var viewModel = new HomeColumnViewModel(_timelineService)
        {
            RemoveColumnCommand = new RelayCommand<ColumnViewModelBase>(RemoveColumn)
        };

        Columns.Add(viewModel);
        await viewModel.LoadAsync();
    }

    public async Task AddSearchColumnAsync()
    {
        var query = $"query-{_searchCounter++}";
        var viewModel = new SearchColumnViewModel(query, _timelineService)
        {
            RemoveColumnCommand = new RelayCommand<ColumnViewModelBase>(RemoveColumn)
        };

        Columns.Add(viewModel);
        await viewModel.LoadAsync();
    }

    public void RemoveColumn(ColumnViewModelBase? column)
    {
        if (column is null)
        {
            return;
        }

        Columns.Remove(column);
    }
}
