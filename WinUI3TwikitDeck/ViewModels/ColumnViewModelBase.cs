using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.ViewModels;

public abstract partial class ColumnViewModelBase(ColumnBase column) : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    public ColumnBase Column { get; } = column;

    public string Header => Column.Header;

    public ObservableCollection<object> Tweets { get; } = [];

    public IRelayCommand<ColumnViewModelBase>? RemoveColumnCommand { get; set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var tweets = await LoadCoreAsync(cancellationToken);
            Tweets.Clear();
            foreach (var tweet in tweets)
            {
                Tweets.Add(tweet);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected abstract Task<IReadOnlyList<object>> LoadCoreAsync(CancellationToken cancellationToken);
}
