using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using WinUI3TwikitDeck.Bridge;
using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.Services;

namespace WinUI3TwikitDeck.ViewModels;

/// <summary>
/// カラム 1 本の共通部分。初回は全件入れ替え（<see cref="LoadAsync"/>）、
/// 更新は新着だけ先頭に差し込み（<see cref="RefreshAsync"/>）、末尾に達したら続きを足す（<see cref="LoadMoreAsync"/>）。
/// 自動更新タイマー・幅・並び順の変更は <see cref="SettingsChanged"/> で通知し、MainViewModel が保存する。
/// </summary>
public abstract partial class ColumnViewModelBase : ObservableObject
{
    /// <summary>自動更新で増え続けないように、更新時に末尾を切り詰める上限（続き取得で足した分も含む）。</summary>
    private const int MaxItems = 1000;

    public const double DefaultWidth = 380;

    public static readonly IReadOnlyList<int> RefreshIntervalChoices = [0, 60, 120, 300];

    public static readonly IReadOnlyList<double> WidthChoices = [300, DefaultWidth, 480];

    private readonly DispatcherQueue? _dispatcher = DispatcherQueue.GetForCurrentThread();
    private DispatcherQueueTimer? _timer;
    private int _loadVersion;

    /// <summary>末尾の続きを取るためのカーソル。null なら続き無し。</summary>
    private string? _nextCursor;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadMoreCommand))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadMoreCommand))]
    private bool _isLoadingMore;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadMoreCommand))]
    private bool _hasMore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RefreshIntervalLabel))]
    private int _refreshIntervalSeconds;

    [ObservableProperty]
    private double _width = DefaultWidth;

    [ObservableProperty]
    private string _lastUpdatedDisplay = string.Empty;

    protected ColumnViewModelBase(ColumnBase column)
    {
        Column = column;
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(), () => !IsLoading);
        LoadMoreCommand = new AsyncRelayCommand(() => LoadMoreAsync(), () => HasMore && !IsLoadingMore && !IsLoading);
        Tweets.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    public ColumnBase Column { get; }

    public virtual string Header => Column.Header;

    /// <summary>ヘッダー下に出す補足（検索語やリスト名など）。無ければ空。</summary>
    public virtual string Subtitle => string.Empty;

    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);

    /// <summary>要素は <see cref="TweetCardModel"/> または <see cref="NotificationCardModel"/>。</summary>
    public ObservableCollection<object> Tweets { get; } = [];

    public bool IsEmpty => Tweets.Count == 0 && !IsLoading;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string RefreshIntervalLabel => FormatInterval(RefreshIntervalSeconds);

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand LoadMoreCommand { get; }

    public IRelayCommand<ColumnViewModelBase>? RemoveColumnCommand { get; set; }

    public IRelayCommand<ColumnViewModelBase>? MoveLeftCommand { get; set; }

    public IRelayCommand<ColumnViewModelBase>? MoveRightCommand { get; set; }

    /// <summary>保存が必要な設定（更新間隔・幅・タブ）が変わった。</summary>
    public event EventHandler? SettingsChanged;

    public static string FormatInterval(int seconds) => seconds switch
    {
        <= 0 => "手動",
        60 => "1分",
        120 => "2分",
        300 => "5分",
        _ when seconds % 60 == 0 => $"{seconds / 60}分",
        _ => $"{seconds}秒",
    };

    public static string FormatWidth(double width) => width switch
    {
        <= 300 => "狭い",
        >= 480 => "広い",
        _ => "標準",
    };

    protected void RaiseSettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);

    partial void OnRefreshIntervalSecondsChanged(int value)
    {
        RestartTimer();
        RaiseSettingsChanged();
    }

    partial void OnWidthChanged(double value) => RaiseSettingsChanged();

    /// <summary>全件入れ替え。続き用カーソルも取り直す。</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var version = Interlocked.Increment(ref _loadVersion);
        IsLoading = true;
        try
        {
            var page = await LoadPageAsync(null, isRefresh: false, cancellationToken);
            if (version != Volatile.Read(ref _loadVersion))
            {
                return;
            }

            Tweets.Clear();
            foreach (var item in page.Items)
            {
                Tweets.Add(item);
            }

            _nextCursor = page.NextCursor;
            HasMore = page.HasMore;
            ErrorMessage = null;
            MarkUpdated();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (version == Volatile.Read(ref _loadVersion))
            {
                ErrorMessage = ex.Message;
                Debug.WriteLine($"Column load failed ({Header}): {ex}");
            }
        }
        finally
        {
            if (version == Volatile.Read(ref _loadVersion))
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// 新着だけを先頭に差し込む。既存のものはカウント・いいね状態を最新にする。
    /// 末尾の続き用カーソルは、まだ続きが取れる間はそのまま残す。
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (Tweets.Count == 0)
        {
            await LoadAsync(cancellationToken);
            return;
        }

        var version = Interlocked.Increment(ref _loadVersion);
        IsLoading = true;
        try
        {
            var page = await LoadPageAsync(null, isRefresh: true, cancellationToken);
            if (version != Volatile.Read(ref _loadVersion))
            {
                return;
            }

            MergeOnTop(page.Items);
            if (_nextCursor is null)
            {
                _nextCursor = page.NextCursor;
                HasMore = page.HasMore;
            }

            ErrorMessage = null;
            MarkUpdated();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (version == Volatile.Read(ref _loadVersion))
            {
                ErrorMessage = ex.Message;
                Debug.WriteLine($"Column refresh failed ({Header}): {ex}");
            }
        }
        finally
        {
            if (version == Volatile.Read(ref _loadVersion))
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>末尾の続きを取得して足す。全件入れ替えや更新が割り込んだら結果は捨てる。</summary>
    public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
    {
        if (!HasMore || IsLoadingMore || IsLoading || string.IsNullOrEmpty(_nextCursor))
        {
            return;
        }

        var version = Volatile.Read(ref _loadVersion);
        var cursor = _nextCursor;
        IsLoadingMore = true;
        try
        {
            var page = await LoadPageAsync(cursor, isRefresh: false, cancellationToken);
            if (version != Volatile.Read(ref _loadVersion))
            {
                return;
            }

            var added = AppendBottom(page.Items);
            _nextCursor = page.NextCursor;
            HasMore = page.HasMore && page.Items.Count > 0;
            ErrorMessage = null;
            Debug.WriteLine($"Column load more ({Header}): +{added} (hasMore={HasMore})");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (version == Volatile.Read(ref _loadVersion))
            {
                ErrorMessage = ex.Message;
                Debug.WriteLine($"Column load more failed ({Header}): {ex}");
            }
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    private void MergeOnTop(IReadOnlyList<object> fetched)
    {
        var existingByKey = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var item in Tweets)
        {
            existingByKey.TryAdd(KeyOf(item), item);
        }

        // 取得した 1 ページ目で固定扱いのツイート。これ以外の既存の固定は解除されたものとして印を外す。
        var fetchedPinnedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in fetched)
        {
            if (item is TweetCardModel { IsPinned: true })
            {
                fetchedPinnedKeys.Add(KeyOf(item));
            }
        }

        foreach (var item in Tweets)
        {
            if (item is TweetCardModel { IsPinned: true } pinned && !fetchedPinnedKeys.Contains(KeyOf(pinned)))
            {
                pinned.IsPinned = false;
            }
        }

        var newItems = new List<object>();
        foreach (var item in fetched)
        {
            var key = KeyOf(item);
            if (existingByKey.TryGetValue(key, out var existing))
            {
                if (existing is TweetCardModel current && item is TweetCardModel fresh)
                {
                    current.ApplyRefresh(fresh);
                    current.IsPinned = fresh.IsPinned;
                }

                continue;
            }

            existingByKey[key] = item;
            newItems.Add(item);
        }

        // 固定ツイートは常に先頭。既存の固定を先頭へ寄せてから、新着をその下に差し込む。
        var pinnedCount = 0;
        for (var i = 0; i < Tweets.Count; i++)
        {
            if (Tweets[i] is TweetCardModel { IsPinned: true })
            {
                if (i != pinnedCount)
                {
                    Tweets.Move(i, pinnedCount);
                }

                pinnedCount++;
            }
        }

        for (var i = newItems.Count - 1; i >= 0; i--)
        {
            var insertAt = newItems[i] is TweetCardModel { IsPinned: true } ? 0 : pinnedCount;
            Tweets.Insert(insertAt, newItems[i]);
            if (insertAt == 0)
            {
                pinnedCount++;
            }
        }

        if (Tweets.Count > MaxItems)
        {
            while (Tweets.Count > MaxItems)
            {
                Tweets.RemoveAt(Tweets.Count - 1);
            }

            // 末尾を捨てたので、そこから先の続きは取り直す
            _nextCursor = null;
            HasMore = false;
        }
    }

    private int AppendBottom(IReadOnlyList<object> fetched)
    {
        var existing = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in Tweets)
        {
            existing.Add(KeyOf(item));
        }

        var added = 0;
        foreach (var item in fetched)
        {
            if (!existing.Add(KeyOf(item)))
            {
                continue;
            }

            Tweets.Add(item);
            added++;
        }

        return added;
    }

    protected virtual string KeyOf(object item) => item switch
    {
        TweetCardModel tweet => "t:" + tweet.DedupKey,
        NotificationCardModel notification => "n:" + notification.Id,
        _ => "o:" + item.GetHashCode(),
    };

    private void MarkUpdated()
        => LastUpdatedDisplay = DateTime.Now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " 更新";

    /// <summary>
    /// 1 ページ取得する。<paramref name="cursor"/> が null なら最新ページ。
    /// <paramref name="isRefresh"/> は一覧を保持したままの最新取得（続き用のカーソルを捨てない）。
    /// </summary>
    protected abstract Task<TimelinePage> LoadPageAsync(string? cursor, bool isRefresh, CancellationToken cancellationToken);

    /// <summary>保存用の定義。派生クラスは種別固有の値を足す。</summary>
    public virtual DeckColumnDefinition ToDefinition() => new()
    {
        RefreshIntervalSeconds = RefreshIntervalSeconds,
        Width = Width,
    };

    /// <summary>カラムを閉じるときに呼ぶ。タイマーを止める。</summary>
    public void Detach()
    {
        StopTimer();
        Interlocked.Increment(ref _loadVersion);
    }

    private void RestartTimer()
    {
        StopTimer();
        if (RefreshIntervalSeconds <= 0 || _dispatcher is null)
        {
            return;
        }

        _timer = _dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds);
        _timer.IsRepeating = true;
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Tick -= OnTimerTick;
        _timer.Stop();
        _timer = null;
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (IsLoading || IsLoadingMore)
        {
            return;
        }

        _ = RefreshAsync();
    }
}
