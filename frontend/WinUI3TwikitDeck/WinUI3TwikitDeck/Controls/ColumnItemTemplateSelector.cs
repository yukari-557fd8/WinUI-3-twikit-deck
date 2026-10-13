using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.Controls;

/// <summary>カラム内の項目がツイートか集約通知かでテンプレートを切り替える。</summary>
public sealed class ColumnItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TweetTemplate { get; set; }

    public DataTemplate? NotificationTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
        => item is NotificationCardModel ? NotificationTemplate : TweetTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
