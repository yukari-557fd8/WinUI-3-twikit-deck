using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.Controls;

public sealed partial class NotificationCard : UserControl
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
        new PropertyMetadata(null));
}
