using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace WinUI3TwikitDeck.Controls;

/// <summary>検索語やユーザー名を 1 行入力する汎用ダイアログ。</summary>
public sealed partial class TextInputDialog : ContentDialog
{
    public TextInputDialog(string title, string description, string placeholder, string? initialText = null)
    {
        InitializeComponent();
        Title = title;
        DescriptionText.Text = description;
        DescriptionText.Visibility = string.IsNullOrEmpty(description) ? Visibility.Collapsed : Visibility.Visible;
        InputBox.PlaceholderText = placeholder;
        InputBox.Text = initialText ?? string.Empty;
        IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(InputBox.Text);
        Opened += (_, _) =>
        {
            InputBox.Focus(FocusState.Programmatic);
            InputBox.SelectAll();
        };
    }

    public string Text => InputBox.Text.Trim();

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        => IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(InputBox.Text);

    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && IsPrimaryButtonEnabled)
        {
            e.Handled = true;
            _enterPressed = true;
            Hide();
        }
    }

    private bool _enterPressed;

    /// <summary>Enter で閉じた場合も「追加」扱いにする。</summary>
    public bool WasConfirmed(ContentDialogResult result)
        => result == ContentDialogResult.Primary || (_enterPressed && Text.Length > 0);
}
