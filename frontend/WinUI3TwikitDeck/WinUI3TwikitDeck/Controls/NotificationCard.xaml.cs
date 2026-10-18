using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.Controls;

public sealed partial class NotificationCard : UserControl, IConversationCard
{
    public NotificationCard()
    {
        InitializeComponent();
    }

    public NotificationCardModel? Notification
    {
        get => (NotificationCardModel?)GetValue(NotificationProperty);
        set => SetValue(NotificationProperty, value);
    }

    public static readonly DependencyProperty NotificationProperty = DependencyProperty.Register(
        nameof(Notification),
        typeof(NotificationCardModel),
        typeof(NotificationCard),
        new PropertyMetadata(null, (d, _) => VisualStateManager.GoToState((NotificationCard)d, "CardNormal", false)));

    string? IConversationCard.CardName => Notification?.Text;

    string? IConversationCard.CardAutomationId => Notification is { Id.Length: > 0 } notification ? "notification-" + notification.Id : null;

    string IConversationCard.CardStatus => string.Empty;

    public bool CanOpenConversation => Notification is { HasTargetTweetId: true };

    /// <summary>いいね・リポストされたツイートの会話を、カードがあるカラムに開く。</summary>
    public void OpenConversation()
    {
        if (Notification is { HasTargetTweetId: true } notification)
        {
            ConversationClick.TryOpen(this, notification.TargetTweetId);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ConversationCardAutomationPeer(this, this);

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
        if (!CanOpenConversation || !ConversationClick.IsCardTap(e, CardBorder))
        {
            return;
        }

        e.Handled = true;
        OpenConversation();
    }
}
