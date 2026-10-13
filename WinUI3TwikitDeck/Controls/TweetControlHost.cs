using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUI3TwikitDeck.Controls;

public sealed class TweetControlHost : UserControl
{
    private readonly ContentControl _content = new();

    public TweetControlHost()
    {
        Content = _content;
    }

    public object? Tweet
    {
        get => GetValue(TweetProperty);
        set => SetValue(TweetProperty, value);
    }

    public static readonly DependencyProperty TweetProperty = DependencyProperty.Register(
        nameof(Tweet),
        typeof(object),
        typeof(TweetControlHost),
        new PropertyMetadata(null, OnTweetChanged));

    private static void OnTweetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TweetControlHost)d).BindTweetToControl(e.NewValue);

    private void BindTweetToControl(object? tweet)
    {
        var tweetControlType = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(a => a.GetType("WinUI_3_twikit.Controls.TweetControl"))
            .FirstOrDefault(t => t is not null);

        if (tweetControlType is not null && Activator.CreateInstance(tweetControlType) is FrameworkElement tweetControl)
        {
            tweetControl.DataContext = tweet;
            _content.Content = tweetControl;
            return;
        }

        _content.Content = new TextBlock
        {
            Text = tweet?.ToString() ?? string.Empty,
            TextWrapping = TextWrapping.Wrap
        };
    }
}
