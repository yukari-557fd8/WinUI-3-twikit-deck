using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI3TwikitDeck.ViewModels;

namespace WinUI3TwikitDeck.Controls;

/// <summary>会話の行がツイート / 取得できなかったツイート / 返信の見出しのどれかでテンプレートを切り替える。</summary>
public sealed class ConversationItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TweetTemplate { get; set; }

    public DataTemplate? MissingTemplate { get; set; }

    public DataTemplate? RepliesHeaderTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item switch
    {
        ConversationEntry { IsRepliesHeader: true } => RepliesHeaderTemplate,
        ConversationEntry { IsMissing: true } => MissingTemplate,
        _ => TweetTemplate,
    };

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
