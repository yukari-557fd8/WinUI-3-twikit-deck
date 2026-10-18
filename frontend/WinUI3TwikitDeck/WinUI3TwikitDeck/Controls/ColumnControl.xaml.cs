using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.ViewModels;

namespace WinUI3TwikitDeck.Controls;

public sealed partial class ColumnControl : UserControl
{
    /// <summary>表示中の会話（<see cref="ColumnViewModelBase.Conversation"/> を最後に反映したもの）。</summary>
    private ConversationViewModel? _shownConversation;

    /// <summary>会話を差し替えてから位置を合わせ終わるまで。この間のスクロールは位置として保存しない。</summary>
    private bool _positioningConversation;

    /// <summary>タイムラインに戻るときに更新ボタンへ移したフォーカスで、デッキを横にスクロールさせない。</summary>
    private bool _suppressRefreshButtonBringIntoView;

    private int _layoutWaits;

    public ColumnControl()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, new PointerEventHandler(Root_PointerPressed), handledEventsToo: true);
        BringIntoViewRequested += Root_BringIntoViewRequested;
    }

    public ColumnViewModelBase? ColumnViewModel
    {
        get => (ColumnViewModelBase?)GetValue(ColumnViewModelProperty);
        set => SetValue(ColumnViewModelProperty, value);
    }

    public static readonly DependencyProperty ColumnViewModelProperty = DependencyProperty.Register(
        nameof(ColumnViewModel),
        typeof(ColumnViewModelBase),
        typeof(ColumnControl),
        new PropertyMetadata(null, OnColumnViewModelChanged));

    /// <summary>末尾からこの距離（px）以内までスクロールしたら続きを取りに行く。</summary>
    private const double LoadMoreThreshold = 600;

    public Visibility FooterVisibility(bool hasMore, bool loadingMore)
        => hasMore || loadingMore ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LoadMoreButtonVisibility(bool hasMore, bool loadingMore)
        => hasMore && !loadingMore ? Visibility.Visible : Visibility.Collapsed;

    private void TweetScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (ColumnViewModel is not { HasMore: true, IsLoadingMore: false, IsLoading: false } column)
        {
            return;
        }

        var scrollViewer = TweetScrollViewer;
        if (scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - LoadMoreThreshold)
        {
            _ = column.LoadMoreAsync();
        }
    }

    public string FooterText(string? updated, string? interval)
    {
        var intervalText = string.IsNullOrEmpty(interval) ? string.Empty : $"自動更新: {interval}";
        if (string.IsNullOrEmpty(updated))
        {
            return intervalText;
        }

        return string.IsNullOrEmpty(intervalText) ? updated : $"{updated} · {intervalText}";
    }

    private static void OnColumnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ColumnControl)d;
        if (e.OldValue is ColumnViewModelBase oldColumn)
        {
            oldColumn.PropertyChanged -= control.Column_PropertyChanged;
        }

        if (e.NewValue is ColumnViewModelBase newColumn)
        {
            newColumn.PropertyChanged += control.Column_PropertyChanged;
        }

        control.ShowConversation((e.NewValue as ColumnViewModelBase)?.Conversation);

        control.TimelineTabView.Visibility = Visibility.Collapsed;
        control.UserTabView.Visibility = Visibility.Collapsed;

        switch (e.NewValue)
        {
            case HomeColumnViewModel home:
                control.TimelineTabView.Visibility = Visibility.Visible;
                SelectTab(control.TimelineTabView, HomeTimelineType.Normalize(home.TimelineType));
                break;
            case UserColumnViewModel user:
                control.UserTabView.Visibility = Visibility.Visible;
                SelectTab(control.UserTabView, user.Tab);
                control.UpdateMediaKindButton(user);
                break;
        }
    }

    private static void SelectTab(TabView tabView, string tag)
    {
        foreach (var item in tabView.TabItems)
        {
            if (item is TabViewItem tab
                && tab.Tag is string itemTag
                && string.Equals(itemTag, tag, StringComparison.Ordinal))
            {
                tabView.SelectedItem = tab;
                return;
            }
        }
    }

    private void UpdateMediaKindButton(UserColumnViewModel user)
    {
        MediaKindButton.Content = user.MediaKindLabel;
        MediaKindButton.Visibility = user.IsMediaTab ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void UserTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ColumnViewModel is not UserColumnViewModel user
            || sender is not TabView tabView
            || tabView.SelectedItem is not TabViewItem tabItem
            || tabItem.Tag is not string tab)
        {
            return;
        }

        await user.SwitchTabAsync(tab);
        UpdateMediaKindButton(user);
    }

    private async void MediaKindItem_Click(object sender, RoutedEventArgs e)
    {
        if (ColumnViewModel is not UserColumnViewModel user
            || sender is not MenuFlyoutItem { Tag: string kind })
        {
            return;
        }

        await user.SwitchMediaKindAsync(kind);
        UpdateMediaKindButton(user);
    }

    private async void TimelineTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ColumnViewModel is not HomeColumnViewModel home
            || sender is not TabView tabView
            || tabView.SelectedItem is not TabViewItem tabItem
            || tabItem.Tag is not string type)
        {
            return;
        }

        await home.SwitchTimelineAsync(type);
    }

    /// <summary>
    /// タブの BringIntoView 要求をここで止める。デッキの横スクロール ScrollViewer まで伝わると、
    /// 右端のカラムのタブに合わせてデッキ全体が横にスクロールしてしまう（タブ列自体はスクロール不要）。
    /// </summary>
    private void TabView_BringIntoViewRequested(UIElement sender, BringIntoViewRequestedEventArgs args)
        => args.Handled = true;

    private void ColumnMenu_Opening(object sender, object e)
    {
        if (ColumnViewModel is not { } column)
        {
            return;
        }

        IntervalManualItem.IsChecked = column.RefreshIntervalSeconds <= 0;
        Interval1Item.IsChecked = column.RefreshIntervalSeconds == 60;
        Interval2Item.IsChecked = column.RefreshIntervalSeconds == 120;
        Interval5Item.IsChecked = column.RefreshIntervalSeconds == 300;

        WidthNarrowItem.IsChecked = column.Width <= 300;
        WidthNormalItem.IsChecked = column.Width > 300 && column.Width < 480;
        WidthWideItem.IsChecked = column.Width >= 480;

        column.MoveLeftCommand?.NotifyCanExecuteChanged();
        column.MoveRightCommand?.NotifyCanExecuteChanged();
    }

    private void IntervalItem_Click(object sender, RoutedEventArgs e)
    {
        if (ColumnViewModel is { } column
            && sender is ToggleMenuFlyoutItem { Tag: string tag }
            && int.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            column.RefreshIntervalSeconds = seconds;
        }
    }

    private void WidthItem_Click(object sender, RoutedEventArgs e)
    {
        if (ColumnViewModel is { } column
            && sender is ToggleMenuFlyoutItem { Tag: string tag }
            && double.TryParse(tag, NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
        {
            column.Width = width;
        }
    }

    // ---- カラム内の会話 ----

    private void Column_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ColumnViewModelBase.Conversation) && ReferenceEquals(sender, ColumnViewModel))
        {
            ShowConversation(ColumnViewModel?.Conversation);
        }
    }

    /// <summary>
    /// 表示する会話を切り替える。前の会話は表示位置を保存しておき、
    /// 新しい会話は初めてなら開いたツイートを先頭に、戻ってきたのなら保存した位置に出す。
    /// </summary>
    private void ShowConversation(ConversationViewModel? conversation)
    {
        if (ReferenceEquals(_shownConversation, conversation))
        {
            return;
        }

        if (_shownConversation is { } previous)
        {
            if (!_positioningConversation)
            {
                SaveConversationPosition(previous);
            }

            previous.PropertyChanged -= Conversation_PropertyChanged;
        }

        SetConversationMode(conversation is not null);
        _shownConversation = conversation;
        _positioningConversation = conversation is not null;
        _layoutWaits = 0;
        ConversationRepeater.ItemsSource = conversation?.Entries;
        ConversationScrollViewer.ChangeView(null, 0, null, disableAnimation: true);
        if (conversation is null)
        {
            return;
        }

        conversation.PropertyChanged += Conversation_PropertyChanged;
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => PositionConversation(conversation));
    }

    /// <summary>
    /// タイムラインと会話の表示を切り替える。会話を開いている間のタイムラインは無効・透明にして重ねる（折りたたまない）。
    /// 隠す側にフォーカスがあれば、先に表示する側へ移す。隠れた要素のフォーカスは次の要素へ移り、
    /// それが他のカラムだとデッキが横にスクロールしてしまうため。
    /// </summary>
    private void SetConversationMode(bool open)
    {
        if (open == (ConversationPanel.Visibility == Visibility.Visible))
        {
            return;
        }

        _suppressRefreshButtonBringIntoView = false;
        if (open)
        {
            ConversationPanel.Visibility = Visibility.Visible;
            if (HasFocusWithin(TimelineHost))
            {
                // 会話側の BringIntoView は ConversationPanel で止まる
                ConversationBackButton.Focus(FocusState.Programmatic);
            }

            TimelineHost.IsEnabled = false;
            TimelineHost.IsHitTestVisible = false;
            TimelineHost.Opacity = 0;
        }
        else
        {
            TimelineHost.IsEnabled = true;
            TimelineHost.IsHitTestVisible = true;
            TimelineHost.Opacity = 1;
            if (HasFocusWithin(ConversationPanel))
            {
                _suppressRefreshButtonBringIntoView = true;
                RefreshButton.Focus(FocusState.Programmatic);
            }

            ConversationPanel.Visibility = Visibility.Collapsed;
        }
    }

    private bool HasFocusWithin(DependencyObject container)
    {
        if (XamlRoot is null)
        {
            return false;
        }

        for (var current = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
             current is not null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, container))
            {
                return true;
            }
        }

        return false;
    }

    private void Conversation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ConversationViewModel conversation || !ReferenceEquals(conversation, _shownConversation))
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(ConversationViewModel.FocalEntry) when !conversation.HasPositioned:
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => PositionConversation(conversation));
                break;
            case nameof(ConversationViewModel.IsLoading) when !conversation.IsLoading && conversation.HasPositioned:
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => _ = LoadMoreRepliesIfNearBottomAsync());
                break;
        }
    }

    /// <summary>
    /// 初めて表示するときは、返信元の流れの下にある開いたツイートを先頭に出す（X の Web 版と同じ）。
    /// 戻ってきたときは保存した位置に戻す。読み込み中なら何もせず、読み込めたら呼び直される。
    /// </summary>
    private void PositionConversation(ConversationViewModel conversation)
    {
        if (!ReferenceEquals(conversation, _shownConversation) || conversation.FocalEntry is not { } focal)
        {
            return;
        }

        if (conversation.HasPositioned)
        {
            ScrollToEntry(conversation.AnchorIndex, conversation.AnchorDelta);
        }
        else
        {
            conversation.HasPositioned = true;
            ScrollToEntry(conversation.Entries.IndexOf(focal), 0);
        }

        _positioningConversation = false;
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => _ = LoadMoreRepliesIfNearBottomAsync());
    }

    /// <summary>
    /// <paramref name="index"/> の行の上端が画面上の <paramref name="delta"/> に来るようにスクロールする。
    /// StartBringIntoView はデッキの横スクロールまで動かしてしまうので、位置を測って ChangeView する。
    /// 行が未作成なら作ってから測り、見積もりで置かれた位置がずれていたらレイアウト後にもう一度合わせる。
    /// </summary>
    private void ScrollToEntry(int index, double delta)
    {
        if (_shownConversation is not { } conversation || index < 0 || index >= conversation.Entries.Count)
        {
            return;
        }

        var viewer = ConversationScrollViewer;
        var element = ConversationRepeater.GetOrCreateElement(index);
        element.UpdateLayout();
        var top = element.TransformToVisual(viewer).TransformPoint(new Point(0, 0)).Y;
        viewer.ChangeView(null, Math.Max(0, viewer.VerticalOffset + top - delta), null, disableAnimation: true);

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (!ReferenceEquals(conversation, _shownConversation) || ConversationRepeater.TryGetElement(index) is not { } placed)
            {
                return;
            }

            var placedTop = placed.TransformToVisual(viewer).TransformPoint(new Point(0, 0)).Y;
            if (Math.Abs(placedTop - delta) > 1)
            {
                viewer.ChangeView(null, Math.Max(0, viewer.VerticalOffset + placedTop - delta), null, disableAnimation: true);
            }
        });
    }

    /// <summary>画面の上端にある行と、その行の上端の位置を保存する（行の高さが見積もりでも、戻ったとき同じ行に戻れる）。</summary>
    private void SaveConversationPosition(ConversationViewModel conversation)
    {
        var viewer = ConversationScrollViewer;
        for (var i = 0; i < conversation.Entries.Count; i++)
        {
            if (ConversationRepeater.TryGetElement(i) is not FrameworkElement element)
            {
                continue;
            }

            var top = element.TransformToVisual(viewer).TransformPoint(new Point(0, 0)).Y;
            if (top + element.ActualHeight > 0)
            {
                conversation.AnchorIndex = i;
                conversation.AnchorDelta = top;
                return;
            }
        }
    }

    private void ConversationScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_positioningConversation || _shownConversation is not { } conversation)
        {
            return;
        }

        if (!e.IsIntermediate)
        {
            SaveConversationPosition(conversation);
        }

        _ = LoadMoreRepliesIfNearBottomAsync();
    }

    /// <summary>末尾付近までスクロールしたら返信の続きを取る。返信が少なくスクロールできない会話でも続きを確かめる。</summary>
    private async Task LoadMoreRepliesIfNearBottomAsync()
    {
        if (_positioningConversation
            || _shownConversation is not { CanLoadMore: true, IsLoading: false, IsLoadingMore: false } conversation)
        {
            return;
        }

        var viewer = ConversationScrollViewer;
        if (viewer.ViewportHeight <= 0)
        {
            // 表示直後でまだレイアウトされていない（スクロールが起きないので ViewChanged も来ない）。少し後で見直す。
            if (_layoutWaits++ < 20)
            {
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => _ = LoadMoreRepliesIfNearBottomAsync());
            }

            return;
        }

        _layoutWaits = 0;
        if (viewer.ScrollableHeight > 0 && viewer.VerticalOffset < viewer.ScrollableHeight - LoadMoreThreshold)
        {
            return;
        }

        await conversation.LoadMoreRepliesAsync();

        // 足した後もまだ末尾付近なら、レイアウト後にもう一度確かめる
        if (ReferenceEquals(conversation, _shownConversation) && conversation.CanLoadMore)
        {
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => _ = LoadMoreRepliesIfNearBottomAsync());
        }
    }

    private void ConversationBackButton_Click(object sender, RoutedEventArgs e)
        => ColumnViewModel?.GoBack();

    private void OpenMissingTweet_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tweetId })
        {
            ColumnViewModel?.OpenConversation(tweetId);
        }
    }

    /// <summary>マウスの戻るボタン（XButton1）で、会話を開いていればひとつ戻る。</summary>
    private void Root_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ColumnViewModel is { IsConversationOpen: true } column
            && e.GetCurrentPoint(this).Properties.IsXButton1Pressed)
        {
            e.Handled = true;
            column.GoBack();
        }
    }

    /// <summary>会話の中の BringIntoView はここで止める（会話の ScrollViewer では効き、デッキの横スクロールまでは伝えない）。</summary>
    private void ConversationPanel_BringIntoViewRequested(UIElement sender, BringIntoViewRequestedEventArgs args)
        => args.Handled = true;

    private void Root_BringIntoViewRequested(UIElement sender, BringIntoViewRequestedEventArgs args)
    {
        if (_suppressRefreshButtonBringIntoView && ReferenceEquals(args.TargetElement, RefreshButton))
        {
            _suppressRefreshButtonBringIntoView = false;
            args.Handled = true;
        }
    }
}
