using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.Services;

namespace WinUI3TwikitDeck.Controls;

public sealed partial class TweetCard : UserControl
{
    public TweetCard()
    {
        InitializeComponent();
    }

    public TweetCardModel? Tweet
    {
        get => (TweetCardModel?)GetValue(TweetProperty);
        set => SetValue(TweetProperty, value);
    }

    public static readonly DependencyProperty TweetProperty = DependencyProperty.Register(
        nameof(Tweet),
        typeof(TweetCardModel),
        typeof(TweetCard),
        new PropertyMetadata(null));

    /// <summary>いいね済みは塗りつぶしのハート。</summary>
    public string LikeGlyph(bool liked) => liked ? "\uEB52" : "\uEB51";

    private void QuoteBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
        => VisualStateManager.GoToState(this, "PointerOver", true);

    private void QuoteBorder_PointerExited(object sender, PointerRoutedEventArgs e)
        => VisualStateManager.GoToState(this, "Normal", true);

    private async void QuoteBorder_Tapped(object sender, TappedRoutedEventArgs e)
    {
        var quoted = Tweet?.QuotedTweet;
        if (quoted is null || quoted.IsUnavailable || string.IsNullOrEmpty(quoted.Id))
        {
            return;
        }

        e.Handled = true;
        await Launcher.LaunchUriAsync(new Uri($"https://x.com/i/status/{quoted.Id}"));
    }

    private async void ReplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (Tweet is { } tweet)
        {
            await AppServices.ComposeAsync(new ComposeContext(ComposeMode.Reply, tweet));
        }
    }

    private async void LikeButton_Click(object sender, RoutedEventArgs e)
        => await TweetActions.ToggleLikeAsync(Tweet);

    private async void RetweetMenuItem_Click(object sender, RoutedEventArgs e)
        => await TweetActions.ToggleRetweetAsync(Tweet);

    private async void QuoteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Tweet is { } tweet)
        {
            await AppServices.ComposeAsync(new ComposeContext(ComposeMode.Quote, tweet));
        }
    }

    private async void OpenInBrowser_Click(object sender, RoutedEventArgs e)
    {
        if (Tweet is { TweetUrl.Length: > 0 } tweet && Uri.TryCreate(tweet.TweetUrl, UriKind.Absolute, out var uri))
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }
}
