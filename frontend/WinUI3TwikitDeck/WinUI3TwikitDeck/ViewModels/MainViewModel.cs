using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUI3TwikitDeck.Bridge;
using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.Services;

namespace WinUI3TwikitDeck.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ITimelineService _timelineService;
    private bool _restoring;
    private int _saveVersion;

    public MainViewModel(ITimelineService timelineService)
    {
        _timelineService = timelineService;
        AddHomeColumnCommand = new AsyncRelayCommand(() => AddHomeColumnAsync());
        AddNotificationsColumnCommand = new AsyncRelayCommand(() => AddNotificationsColumnAsync(NotificationType.All));
        AddMentionsColumnCommand = new AsyncRelayCommand(() => AddNotificationsColumnAsync(NotificationType.Mentions));
        AddSearchColumnCommand = new AsyncRelayCommand<string>(query => AddSearchColumnAsync(query ?? string.Empty));
        AddUserColumnCommand = new AsyncRelayCommand<string>(name => AddUserColumnAsync(name ?? string.Empty));
        RefreshAllCommand = new AsyncRelayCommand(RefreshAllAsync);
    }

    public ObservableCollection<ColumnViewModelBase> Columns { get; } = [];

    public IAsyncRelayCommand AddHomeColumnCommand { get; }

    public IAsyncRelayCommand AddNotificationsColumnCommand { get; }

    public IAsyncRelayCommand AddMentionsColumnCommand { get; }

    public IAsyncRelayCommand<string> AddSearchColumnCommand { get; }

    public IAsyncRelayCommand<string> AddUserColumnCommand { get; }

    public IAsyncRelayCommand RefreshAllCommand { get; }

    /// <summary>保存済みのカラム構成を復元する。無ければホームカラムを 1 本作る。</summary>
    public async Task InitializeAsync()
    {
        var saved = DeckLayoutStore.Load();
        if (saved is null || saved.Count == 0)
        {
            await AddHomeColumnAsync();
            return;
        }

        _restoring = true;
        var created = new List<ColumnViewModelBase>();
        try
        {
            foreach (var definition in saved)
            {
                var column = CreateFromDefinition(definition);
                if (column is null)
                {
                    continue;
                }

                Attach(column);
                Columns.Add(column);
                created.Add(column);
            }
        }
        finally
        {
            _restoring = false;
        }

        if (created.Count == 0)
        {
            await AddHomeColumnAsync();
            return;
        }

        Debug.WriteLine($"カラム構成を復元: {created.Count} 本 ({DeckLayoutStore.Path})");
        await Task.WhenAll(created.Select(c => c.LoadAsync()));
    }

    private ColumnViewModelBase? CreateFromDefinition(DeckColumnDefinition definition)
    {
        ColumnViewModelBase? column = definition.Type?.ToLowerInvariant() switch
        {
            "home" => new HomeColumnViewModel(_timelineService, definition.TimelineType),
            "search" when !string.IsNullOrWhiteSpace(definition.Query) => new SearchColumnViewModel(definition.Query, _timelineService),
            "notifications" => new NotificationsColumnViewModel(definition.NotificationType ?? NotificationType.All, _timelineService),
            "list" when !string.IsNullOrWhiteSpace(definition.ListId) => new ListColumnViewModel(definition.ListId, definition.ListName ?? definition.ListId, _timelineService),
            "user" when !string.IsNullOrWhiteSpace(definition.ScreenName) => new UserColumnViewModel(definition.ScreenName, _timelineService, definition.UserTab, definition.MediaKind),
            _ => null,
        };

        if (column is null)
        {
            return null;
        }

        column.Width = ColumnViewModelBase.WidthChoices.Contains(definition.Width) ? definition.Width : ColumnViewModelBase.DefaultWidth;
        column.RefreshIntervalSeconds = Math.Max(0, definition.RefreshIntervalSeconds);
        return column;
    }

    public Task<ColumnViewModelBase> AddHomeColumnAsync(string? timelineType = null)
        => AddColumnAsync(new HomeColumnViewModel(_timelineService, timelineType));

    public Task<ColumnViewModelBase> AddSearchColumnAsync(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return Task.FromResult<ColumnViewModelBase>(null!);
        }

        return AddColumnAsync(new SearchColumnViewModel(trimmed, _timelineService));
    }

    public Task<ColumnViewModelBase> AddNotificationsColumnAsync(string notificationType)
        => AddColumnAsync(new NotificationsColumnViewModel(notificationType, _timelineService));

    public Task<ColumnViewModelBase> AddListColumnAsync(TwitterListModel list)
        => AddColumnAsync(new ListColumnViewModel(list.Id, list.Name, _timelineService));

    public Task<ColumnViewModelBase> AddUserColumnAsync(string screenName)
    {
        var trimmed = screenName.Trim().TrimStart('@');
        if (trimmed.Length == 0)
        {
            return Task.FromResult<ColumnViewModelBase>(null!);
        }

        return AddColumnAsync(new UserColumnViewModel(trimmed, _timelineService));
    }

    /// <summary>ログイン中ユーザーのリスト一覧（カラム追加ダイアログ用）。</summary>
    public Task<IReadOnlyList<TwitterListModel>> LoadListsAsync() => _timelineService.LoadListsAsync();

    private async Task<ColumnViewModelBase> AddColumnAsync(ColumnViewModelBase column)
    {
        Attach(column);
        Columns.Add(column);
        SaveLayout();
        await column.LoadAsync();
        return column;
    }

    private void Attach(ColumnViewModelBase column)
    {
        column.RemoveColumnCommand = new RelayCommand<ColumnViewModelBase>(RemoveColumn);
        column.MoveLeftCommand = new RelayCommand<ColumnViewModelBase>(c => MoveColumn(c, -1), c => c is not null && Columns.IndexOf(c) > 0);
        column.MoveRightCommand = new RelayCommand<ColumnViewModelBase>(c => MoveColumn(c, +1), c => c is not null && Columns.IndexOf(c) < Columns.Count - 1);
        column.SettingsChanged += (_, _) => SaveLayout();
    }

    public void RemoveColumn(ColumnViewModelBase? column)
    {
        if (column is null)
        {
            return;
        }

        column.Detach();
        Columns.Remove(column);
        NotifyMoveCommands();
        SaveLayout();
    }

    public void MoveColumn(ColumnViewModelBase? column, int delta)
    {
        if (column is null)
        {
            return;
        }

        var index = Columns.IndexOf(column);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Columns.Count)
        {
            return;
        }

        Columns.Move(index, target);
        NotifyMoveCommands();
        SaveLayout();
    }

    private void NotifyMoveCommands()
    {
        foreach (var column in Columns)
        {
            column.MoveLeftCommand?.NotifyCanExecuteChanged();
            column.MoveRightCommand?.NotifyCanExecuteChanged();
        }
    }

    public Task RefreshAllAsync()
        => Task.WhenAll(Columns.Where(c => !c.IsLoading).Select(c => c.RefreshAsync()).ToList());

    /// <summary>少し待ってからまとめて保存する（連続した並び替えや幅変更で何度も書かない）。</summary>
    private async void SaveLayout()
    {
        if (_restoring)
        {
            return;
        }

        var version = ++_saveVersion;
        try
        {
            await Task.Delay(500);
            if (version != _saveVersion)
            {
                return;
            }

            var definitions = Columns.Select(c => c.ToDefinition()).ToList();
            await Task.Run(() => DeckLayoutStore.Save(definitions));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"カラム構成の保存に失敗: {ex.Message}");
        }
    }
}
