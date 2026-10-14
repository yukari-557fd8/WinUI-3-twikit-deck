using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUI3TwikitDeck.Bridge;
using WinUI3TwikitDeck.Helpers;
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

        // 拡大率の違うモニターへ移動したときなどに、表示中のカードの画像を新しい倍率で取り直す
        DisplayScale.Changed += _ => NotifyDisplayScaleChanged();
    }

    public ObservableCollection<ColumnViewModelBase> Columns { get; } = [];

    /// <summary>カラム同士の間隔（DIP）。MainWindow.xaml の StackLayout Spacing と合わせる。</summary>
    public const double ColumnSpacing = 12;

    /// <summary>
    /// カラム幅をウィンドウに合わせる。オンのときは各カラムの幅設定（狭い / 標準 / 広い）を比率として
    /// カラム領域を配分する（<see cref="UpdateColumnWidths"/>）。保存される。
    /// </summary>
    [ObservableProperty]
    private bool _fitColumnsToWindow;

    private double _viewportWidth;

    /// <summary>カラム領域の幅（DIP）。MainWindow がサイズ変更のたびに渡す。保存はしない。</summary>
    public double ViewportWidth
    {
        get => _viewportWidth;
        set
        {
            if (Math.Abs(_viewportWidth - value) < 0.5)
            {
                return;
            }

            _viewportWidth = value;
            if (FitColumnsToWindow)
            {
                UpdateColumnWidths();
            }
        }
    }

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
        if (saved is null || saved.Columns.Count == 0)
        {
            await AddHomeColumnAsync();
            return;
        }

        _restoring = true;
        var created = new List<ColumnViewModelBase>();
        try
        {
            FitColumnsToWindow = saved.FitColumnsToWindow;
            foreach (var definition in saved.Columns)
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

        UpdateColumnWidths();
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
        UpdateColumnWidths();
        SaveLayout();
        await column.LoadAsync();
        return column;
    }

    private void Attach(ColumnViewModelBase column)
    {
        column.RemoveColumnCommand = new RelayCommand<ColumnViewModelBase>(RemoveColumn);
        column.MoveLeftCommand = new RelayCommand<ColumnViewModelBase>(c => MoveColumn(c, -1), c => c is not null && Columns.IndexOf(c) > 0);
        column.MoveRightCommand = new RelayCommand<ColumnViewModelBase>(c => MoveColumn(c, +1), c => c is not null && Columns.IndexOf(c) < Columns.Count - 1);
        column.SettingsChanged += (_, _) =>
        {
            // 幅設定（狭い / 標準 / 広い）が変わると、ウィンドウに合わせるモードでは配分も変わる
            UpdateColumnWidths();
            SaveLayout();
        };
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
        UpdateColumnWidths();
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

    partial void OnFitColumnsToWindowChanged(bool value)
    {
        UpdateColumnWidths();
        SaveLayout();
    }

    /// <summary>
    /// 各カラムの表示幅を決める。
    /// 固定幅モードでは設定した幅そのまま。ウィンドウに合わせるモードでは、設定幅（狭い / 標準 / 広い）を比率として
    /// カラム領域（<see cref="ViewportWidth"/>）を配分し、<see cref="ColumnViewModelBase.MinFitWidth"/>〜
    /// <see cref="ColumnViewModelBase.MaxFitWidth"/> に収める。入り切らない分は横スクロールになる。
    /// </summary>
    public void UpdateColumnWidths()
    {
        if (Columns.Count == 0)
        {
            return;
        }

        if (!FitColumnsToWindow || _viewportWidth <= 0)
        {
            foreach (var column in Columns)
            {
                column.DisplayWidth = column.Width;
            }

            return;
        }

        var available = Math.Max(0, _viewportWidth - ColumnSpacing * (Columns.Count - 1));
        var totalWeight = Columns.Sum(c => Math.Max(1, c.Width));
        foreach (var column in Columns)
        {
            var share = available * (Math.Max(1, column.Width) / totalWeight);
            column.DisplayWidth = Math.Floor(Math.Clamp(share, ColumnViewModelBase.MinFitWidth, ColumnViewModelBase.MaxFitWidth));
        }
    }

    /// <summary>表示倍率が変わった。表示中のカードの画像を新しい倍率でデコードし直させる。</summary>
    private void NotifyDisplayScaleChanged()
    {
        foreach (var column in Columns)
        {
            foreach (var item in column.Tweets)
            {
                switch (item)
                {
                    case TweetCardModel tweet:
                        tweet.NotifyDisplayScaleChanged();
                        break;
                    case NotificationCardModel notification:
                        notification.NotifyDisplayScaleChanged();
                        break;
                }
            }
        }
    }

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

            var layout = new DeckLayout
            {
                FitColumnsToWindow = FitColumnsToWindow,
                Columns = Columns.Select(c => c.ToDefinition()).ToList(),
            };
            await Task.Run(() => DeckLayoutStore.Save(layout));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"カラム構成の保存に失敗: {ex.Message}");
        }
    }
}
