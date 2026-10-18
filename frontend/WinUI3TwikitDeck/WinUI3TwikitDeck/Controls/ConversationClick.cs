using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace WinUI3TwikitDeck.Controls;

/// <summary>
/// カードのクリックで、そのカードがあるカラムに会話を開く（WinUI-3-twikit より移植）。
/// <see cref="TweetCard"/> と <see cref="NotificationCard"/> で共通。
/// </summary>
public static class ConversationClick
{
    /// <summary>
    /// カードの余白や本文のタップか。ボタン・入力欄・<paramref name="exclude"/>（引用など自分の動作を持つ部分）・本文のリンクの上、
    /// またはメディアのように自分でタップを処理した（Handled にした）部品の上なら false。
    /// </summary>
    /// <param name="card">クリックを受けるカードの要素。ここより上はたどらない。</param>
    public static bool IsCardTap(TappedRoutedEventArgs e, DependencyObject card, DependencyObject? exclude = null)
    {
        TweetTextBlock? bodyText = null;
        var onSelectableText = false;
        for (var current = e.OriginalSource as DependencyObject;
             current is not null && !ReferenceEquals(current, card);
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is ButtonBase or TextBox || (exclude is not null && ReferenceEquals(current, exclude)))
            {
                return false;
            }

            bodyText ??= current as TweetTextBlock;
            onSelectableText |= current is TextBlock { IsTextSelectionEnabled: true };
        }

        if (bodyText is not null)
        {
            return !bodyText.IsLinkAt(e.GetPosition(bodyText));
        }

        // 選択できる TextBlock はタップを Handled にするが、クリックの処理ではない。
        return !e.Handled || onSelectableText;
    }

    /// <summary><paramref name="source"/> を含むカラムに会話を開く。カラムの外にあるときは何もしない。</summary>
    public static void TryOpen(DependencyObject source, string tweetId)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ColumnControl { ColumnViewModel: { } column })
            {
                column.OpenConversation(tweetId);
                return;
            }
        }
    }
}

/// <summary>クリックで会話を開くカード。<see cref="ConversationCardAutomationPeer"/> が読む。</summary>
public interface IConversationCard
{
    /// <summary>読み上げる名前（本文の要約など）。</summary>
    string? CardName { get; }

    /// <summary>UI オートメーションの ID（<c>tweet-{id}</c> など）。</summary>
    string? CardAutomationId { get; }

    /// <summary>補足の状態（開いているツイートなど）。無ければ空。</summary>
    string CardStatus { get; }

    bool CanOpenConversation { get; }

    void OpenConversation();
}

/// <summary>
/// クリックで会話を開くカードの UI オートメーション。一覧の項目として名前と ID を出し、
/// Invoke でクリックと同じく会話を開く（スクリーンリーダーや UI オートメーションからも開ける）。
/// </summary>
public sealed class ConversationCardAutomationPeer(FrameworkElement owner, IConversationCard card)
    : FrameworkElementAutomationPeer(owner), IInvokeProvider
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string GetClassNameCore() => Owner.GetType().Name;

    protected override string GetNameCore() => card.CardName is { Length: > 0 } name ? name : base.GetNameCore();

    protected override string GetAutomationIdCore()
        => card.CardAutomationId is { Length: > 0 } id ? id : base.GetAutomationIdCore();

    protected override string GetItemStatusCore() => card.CardStatus;

    protected override object GetPatternCore(PatternInterface patternInterface)
        => patternInterface == PatternInterface.Invoke && card.CanOpenConversation ? this : base.GetPatternCore(patternInterface);

    public void Invoke()
    {
        // 会話を開いている間の裏のタイムラインは無効になっている
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }

        card.OpenConversation();
    }
}
