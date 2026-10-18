using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.Services;

namespace WinUI3TwikitDeck.Controls;

public sealed partial class TweetCard : UserControl, IConversationCard
{
    public TweetCard()
    {
        InitializeComponent();
        // 本文（選択できる TextBlock）はタップを Handled にするので、Handled 済みも受け取って判断する。
        CardBorder.AddHandler(TappedEvent, new TappedEventHandler(CardBorder_Tapped), handledEventsToo: true);
        // 読み込み前に IsFocal が設定されると GoToState が効かないので、表示時にも合わせる。
        Loaded += (_, _) => ApplyFocalState();
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
        new PropertyMetadata(null, OnTweetChanged));

    /// <summary>カラム内の会話で開いているツイート。枠を強調し、ホバー表示もクリックでの遷移もしない。</summary>
    public bool IsFocal
    {
        get => (bool)GetValue(IsFocalProperty);
        set => SetValue(IsFocalProperty, value);
    }

    public static readonly DependencyProperty IsFocalProperty = DependencyProperty.Register(
        nameof(IsFocal),
        typeof(bool),
        typeof(TweetCard),
        new PropertyMetadata(false, (d, _) => ((TweetCard)d).ApplyFocalState()));

    /// <summary>「返信先: @名前」を出すか。会話の中は返信の連鎖を線で表すので出さない。</summary>
    public bool ShowReplyTo
    {
        get => (bool)GetValue(ShowReplyToProperty);
        set => SetValue(ShowReplyToProperty, value);
    }

    public static readonly DependencyProperty ShowReplyToProperty = DependencyProperty.Register(
        nameof(ShowReplyTo),
        typeof(bool),
        typeof(TweetCard),
        new PropertyMetadata(true));

    /// <summary>いいね済みは塗りつぶしのハート。</summary>
    public string LikeGlyph(bool liked) => liked ? "" : "";

    public bool ReplyToVisible(bool hasReplyTo, bool showReplyTo) => hasReplyTo && showReplyTo;

    string? IConversationCard.CardName => Tweet?.Summary;

    string? IConversationCard.CardAutomationId => Tweet is { Id.Length: > 0 } tweet ? "tweet-" + tweet.Id : null;

    string IConversationCard.CardStatus => IsFocal ? "開いているツイート" : string.Empty;

    public bool CanOpenConversation => !IsFocal && Tweet is { Id.Length: > 0 };

    /// <summary>このツイートの会話を、カードがあるカラムに開く。</summary>
    public void OpenConversation()
    {
        if (CanOpenConversation && Tweet is { } tweet)
        {
            ConversationClick.TryOpen(this, tweet.Id);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ConversationCardAutomationPeer(this, this);

    private static void OnTweetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // ItemsRepeater でカードが別のツイートに使い回されたとき、前のホバー表示を残さない。
        var card = (TweetCard)d;
        VisualStateManager.GoToState(card, "CardNormal", false);
        VisualStateManager.GoToState(card, "QuoteNormal", false);
    }

    private void ApplyFocalState()
    {
        VisualStateManager.GoToState(this, IsFocal ? "Focal" : "NotFocal", false);
        if (IsFocal)
        {
            VisualStateManager.GoToState(this, "CardNormal", false);
        }
    }

    private void CardBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (CanOpenConversation)
        {
            VisualStateManager.GoToState(this, "CardPointerOver", true);
        }
    }

    private void CardBorder_PointerExited(object sender, PointerRoutedEventArgs e)
        => VisualStateManager.GoToState(this, "CardNormal", true);

    private void CardBorder_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (!CanOpenConversation || !ConversationClick.IsCardTap(e, CardBorder, QuoteBorder))
        {
            return;
        }

        e.Handled = true;
        OpenConversation();
    }

    private void QuoteBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
        => VisualStateManager.GoToState(this, "QuotePointerOver", true);

    private void QuoteBorder_PointerExited(object sender, PointerRoutedEventArgs e)
        => VisualStateManager.GoToState(this, "QuoteNormal", true);

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
