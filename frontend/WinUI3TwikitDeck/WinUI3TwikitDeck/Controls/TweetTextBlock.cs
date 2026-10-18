using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Windows.Foundation;
using Windows.System;
using WinUI3TwikitDeck.Helpers;
using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.Services;

namespace WinUI3TwikitDeck.Controls;

/// <summary>
/// 本文の URL / ハッシュタグ / メンションをリンクにして表示する。
/// URL は既定のブラウザで開き、ハッシュタグは検索カラム、メンションはユーザーカラムを追加する。
/// </summary>
public sealed class TweetTextBlock : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(TweetTextBlock),
        new PropertyMetadata(string.Empty, (d, _) => ((TweetTextBlock)d).Rebuild()));

    public static readonly DependencyProperty UrlsProperty = DependencyProperty.Register(
        nameof(Urls),
        typeof(object),
        typeof(TweetTextBlock),
        new PropertyMetadata(null, (d, _) => ((TweetTextBlock)d).Rebuild()));

    public static readonly DependencyProperty TextFontSizeProperty = DependencyProperty.Register(
        nameof(TextFontSize),
        typeof(double),
        typeof(TweetTextBlock),
        new PropertyMetadata(15.0, (d, e) => ((TweetTextBlock)d)._textBlock.FontSize = (double)e.NewValue));

    private readonly TextBlock _textBlock;

    public TweetTextBlock()
    {
        _textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.WrapWholeWords,
            IsTextSelectionEnabled = true,
            FontSize = 15,
            LineHeight = 20,
        };
        Content = _textBlock;
        IsTabStop = false;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary><see cref="IReadOnlyList{UrlEntityModel}"/>。t.co を展開後 URL で表示するために使う。</summary>
    public object? Urls
    {
        get => GetValue(UrlsProperty);
        set => SetValue(UrlsProperty, value);
    }

    public double TextFontSize
    {
        get => (double)GetValue(TextFontSizeProperty);
        set => SetValue(TextFontSizeProperty, value);
    }

    /// <summary>
    /// <paramref name="point"/>（この要素の座標）がリンクの上か（WinUI-3-twikit より移植）。
    /// カードのクリックで会話を開くとき、リンクのクリックと区別するために使う。
    /// </summary>
    public bool IsLinkAt(Point point)
    {
        foreach (var link in _textBlock.Inlines.OfType<Hyperlink>())
        {
            // 文字ごとの位置を行ごとにまとめ、行ごとの範囲で判定する（リンクが折り返していてもよい）。
            // リンク末尾の位置（直後の文字の左端）まで含めると、最後の文字の右端までが範囲になる。
            var end = link.ContentEnd.Offset;
            Rect? line = null;
            for (var position = link.ContentStart;
                 position is not null && position.Offset <= end;
                 position = position.GetPositionAtOffset(1, LogicalDirection.Forward))
            {
                var rect = position.GetCharacterRect(LogicalDirection.Forward);
                if (line is { } current && Math.Abs(current.Y - rect.Y) < 1)
                {
                    current.Union(rect);
                    line = current;
                    continue;
                }

                if (line is { } finished && LineContains(finished, point))
                {
                    return true;
                }

                line = rect;
            }

            if (line is { } last && LineContains(last, point))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LineContains(Rect line, Point point)
        => point.X >= line.Left && point.X <= line.Right && point.Y >= line.Top && point.Y <= line.Bottom;

    private void Rebuild()
    {
        _textBlock.Inlines.Clear();
        var text = Text;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (var segment in TweetTextParser.Parse(text, Urls as IReadOnlyList<UrlEntityModel>))
        {
            if (segment.Kind == TextSegmentKind.Text)
            {
                _textBlock.Inlines.Add(new Run { Text = segment.Text });
                continue;
            }

            var link = new Hyperlink
            {
                UnderlineStyle = UnderlineStyle.None,
            };
            link.Inlines.Add(new Run { Text = segment.Text });
            var captured = segment;
            link.Click += async (_, _) => await ActivateAsync(captured);
            _textBlock.Inlines.Add(link);
        }
    }

    private static async System.Threading.Tasks.Task ActivateAsync(TextSegment segment)
    {
        try
        {
            switch (segment.Kind)
            {
                case TextSegmentKind.Url:
                    if (Uri.TryCreate(segment.Target, UriKind.Absolute, out var uri))
                    {
                        await Launcher.LaunchUriAsync(uri);
                    }

                    break;
                case TextSegmentKind.Hashtag:
                    if (AppServices.IsConfigured)
                    {
                        await AppServices.Deck.AddSearchColumnAsync("#" + segment.Target);
                    }

                    break;
                case TextSegmentKind.Mention:
                    if (AppServices.IsConfigured)
                    {
                        await AppServices.Deck.AddUserColumnAsync(segment.Target);
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            AppNotifications.Error($"リンクを開けませんでした: {ex.Message}");
        }
    }
}
