using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.Services;

namespace WinUI3TwikitDeck.ViewModels;

/// <summary>
/// カラム内に開いた会話（WinUI-3-twikit の会話ページより移植）。
/// 上から返信元の流れ（古い順）、開いたツイート、返信（X の会話のまとまりごと）を並べる。
/// 会話を開くたびに作り、<see cref="ColumnViewModelBase"/> の戻る履歴に積むので、戻ったときは読み込み済みの内容と位置をそのまま使う。
/// </summary>
public sealed partial class ConversationViewModel : ObservableObject
{
    private readonly ITimelineService _timelineService;
    private readonly HashSet<string> _seenIds = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _cancellation = new();
    private int _loadVersion;
    private bool _hasLoaded;
    private string? _nextCursor;
    private int _replyCount;
    private ConversationEntry? _repliesHeader;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingMore;

    [ObservableProperty]
    private bool _canLoadMore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>開いたツイートの行。読み込みが終わるまで null。</summary>
    [ObservableProperty]
    private ConversationEntry? _focalEntry;

    public ConversationViewModel(string tweetId, ITimelineService timelineService)
    {
        TweetId = tweetId;
        _timelineService = timelineService;
    }

    /// <summary>開いたツイートの ID。</summary>
    public string TweetId { get; }

    public ObservableCollection<ConversationEntry> Entries { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>表示中のツイート（返信元・開いたツイート・返信）。</summary>
    public IEnumerable<TweetCardModel> Tweets => Entries.Select(entry => entry.Tweet).OfType<TweetCardModel>();

    /// <summary>開いたツイートの位置までスクロールしたか。済んでいれば、戻ってきたときは <see cref="AnchorIndex"/> の位置に戻す。</summary>
    public bool HasPositioned { get; set; }

    /// <summary>最後に表示していた位置。画面の上端にあった行と、その行の上端の画面上の位置（DIP、上にはみ出していれば負）。</summary>
    public int AnchorIndex { get; set; }

    public double AnchorDelta { get; set; }

    public bool Contains(string tweetId)
        => Tweets.Any(tweet => string.Equals(tweet.Id, tweetId, StringComparison.Ordinal));

    partial void OnIsLoadingMoreChanged(bool value) => UpdateNoReplies();

    partial void OnCanLoadMoreChanged(bool value) => UpdateNoReplies();

    /// <summary>
    /// 返信が 1 件も無いと確定したら、返信の見出しの下に「返信はありません」を出す。
    /// 一覧の末尾に置くと会話が短いときに見出しから離れてしまうので見出しの行に出す。
    /// </summary>
    private void UpdateNoReplies()
    {
        if (_repliesHeader is not null)
        {
            _repliesHeader.ShowNoReplies = !IsLoadingMore && !CanLoadMore && _replyCount == 0;
        }
    }

    /// <summary>会話全体を取得する（1 回だけ）。</summary>
    public Task LoadAsync() => _hasLoaded || IsLoading ? Task.CompletedTask : LoadFirstPageAsync();

    /// <summary>1 ページ目を取り直して並べ直す（更新ボタン・返信の投稿後）。取得に失敗しても表示中の会話は残す。</summary>
    public Task ReloadAsync() => IsLoading ? Task.CompletedTask : LoadFirstPageAsync();

    private async Task LoadFirstPageAsync()
    {
        var version = ++_loadVersion;
        IsLoading = true;
        try
        {
            var page = await _timelineService.LoadConversationAsync(TweetId, null, _cancellation.Token);
            if (version != _loadVersion)
            {
                return;
            }

            if (page.Focal is null)
            {
                throw new InvalidOperationException("会話を読み込めませんでした");
            }

            BuildEntries(page);
            ErrorMessage = null;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (version != _loadVersion)
            {
                return;
            }

            Debug.WriteLine($"Conversation load failed ({TweetId}): {ex}");
            if (FocalEntry is null)
            {
                ErrorMessage = ex.Message;
            }
            else
            {
                AppNotifications.Error($"会話を更新できませんでした: {ex.Message}");
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                _hasLoaded = true;
                IsLoading = false;
            }
        }
    }

    /// <summary>返信の続きを取得する。全体の取り直しが割り込んだら結果は捨てる。</summary>
    public async Task LoadMoreRepliesAsync()
    {
        if (IsLoading || IsLoadingMore || !CanLoadMore || string.IsNullOrEmpty(_nextCursor))
        {
            return;
        }

        var version = _loadVersion;
        var requestedCursor = _nextCursor;
        IsLoadingMore = true;
        try
        {
            var page = await _timelineService.LoadConversationAsync(TweetId, requestedCursor, _cancellation.Token);
            if (version != _loadVersion)
            {
                return;
            }

            var added = AddReplyThreads(page.Replies);
            _nextCursor = page.NextCursor;

            // 新規 0 件、またはカーソルが進まない場合は打ち切り（同一ページの再取得ループ防止）
            CanLoadMore = added > 0
                && !string.IsNullOrEmpty(_nextCursor)
                && !string.Equals(_nextCursor, requestedCursor, StringComparison.Ordinal);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
            {
                // 続きの取得失敗は、取得済みの会話を残したまま通知だけする（スクロールのたびに再送しない）。
                Debug.WriteLine($"Conversation load more failed ({TweetId}): {ex}");
                AppNotifications.Error($"返信の続きを読み込めませんでした: {ex.Message}");
                CanLoadMore = false;
            }
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    /// <summary>閉じた会話の読み込みを止め、後から届いた結果を捨てる。</summary>
    public void Cancel()
    {
        _loadVersion++;
        _cancellation.Cancel();
    }

    private void BuildEntries(ConversationPage page)
    {
        Entries.Clear();
        _seenIds.Clear();
        _replyCount = 0;

        ConversationEntry? previous = null;
        foreach (var link in page.Ancestors)
        {
            previous = AppendChainLink(link, previous, isFocal: false);
        }

        FocalEntry = AppendChainLink(page.Focal!, previous, isFocal: true);

        _repliesHeader = new ConversationEntry { IsRepliesHeader = true };
        Entries.Add(_repliesHeader);

        AddReplyThreads(page.Replies);
        _nextCursor = page.NextCursor;
        CanLoadMore = !string.IsNullOrEmpty(_nextCursor);
        UpdateNoReplies();
    }

    /// <summary>返信元の流れに 1 件足す。返信先を X が返さなかった場合は、その位置に「取得できなかったツイート」を挟む。</summary>
    private ConversationEntry AppendChainLink(ConversationLink link, ConversationEntry? previous, bool isFocal)
    {
        if (!string.IsNullOrEmpty(link.MissingParentId))
        {
            previous = Append(new ConversationEntry { MissingTweetId = link.MissingParentId, CanOpenMissing = true }, previous);
        }

        if (!string.IsNullOrEmpty(link.Id))
        {
            _seenIds.Add(link.Id);
        }

        if (link.Tweet is not { } tweet)
        {
            return Append(new ConversationEntry
            {
                MissingTweetId = link.Id,
                MissingText = string.IsNullOrWhiteSpace(link.UnavailableText) ? "このツイートは表示できません" : link.UnavailableText,
            }, previous);
        }

        return Append(new ConversationEntry { Tweet = tweet, IsFocal = isFocal }, previous);
    }

    /// <summary>行を末尾に足し、<paramref name="replyTo"/>（返信先の行）があれば線でつなぐ。</summary>
    private ConversationEntry Append(ConversationEntry entry, ConversationEntry? replyTo)
    {
        if (replyTo is not null)
        {
            replyTo.HasLineBelow = true;
            entry.HasLineAbove = true;
        }

        Entries.Add(entry);
        return entry;
    }

    /// <summary>返信を会話のまとまりごとに足す。まとまりの中は上から順に返信の連鎖なので線でつなぐ。</summary>
    private int AddReplyThreads(IReadOnlyList<IReadOnlyList<TweetCardModel>> threads)
    {
        var added = 0;
        foreach (var thread in threads)
        {
            ConversationEntry? previous = null;
            foreach (var tweet in thread)
            {
                if (string.IsNullOrEmpty(tweet.Id) || !_seenIds.Add(tweet.Id))
                {
                    continue;
                }

                previous = Append(new ConversationEntry { Tweet = tweet }, previous);
                added++;
            }
        }

        _replyCount += added;
        UpdateNoReplies();
        return added;
    }
}
