using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.ViewModels;

namespace WinUI3TwikitDeck.Controls;

public sealed partial class ColumnControl : UserControl
{
    public ColumnControl()
    {
        InitializeComponent();
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
}
