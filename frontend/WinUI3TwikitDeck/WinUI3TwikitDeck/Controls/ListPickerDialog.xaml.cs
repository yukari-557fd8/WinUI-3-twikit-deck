using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.Controls;

/// <summary>ログイン中ユーザーのリストから 1 つ選ぶ。</summary>
public sealed partial class ListPickerDialog : ContentDialog
{
    private readonly Func<Task<IReadOnlyList<TwitterListModel>>> _loadLists;
    private bool _doubleTapped;

    public ListPickerDialog(Func<Task<IReadOnlyList<TwitterListModel>>> loadLists)
    {
        InitializeComponent();
        _loadLists = loadLists;
        IsPrimaryButtonEnabled = false;
        Opened += OnOpened;
    }

    public TwitterListModel? SelectedList => ListsView.SelectedItem as TwitterListModel;

    public bool WasConfirmed(ContentDialogResult result)
        => SelectedList is not null && (result == ContentDialogResult.Primary || _doubleTapped);

    private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        try
        {
            var lists = await _loadLists();
            ListsView.ItemsSource = lists;
            EmptyText.Visibility = lists.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            EmptyText.Text = $"リストを取得できませんでした: {ex.Message}";
            EmptyText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private void ListsView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => IsPrimaryButtonEnabled = SelectedList is not null;

    private void ListsView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (SelectedList is null)
        {
            return;
        }

        _doubleTapped = true;
        Hide();
    }
}
