using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.Controls;

/// <summary>
/// 画像グリッド。1 枚: フル幅 / 2 枚: 左右 50% / 3 枚: 左 50% + 右上下 / 4 枚: 2x2。
/// 動画・GIF にはサムネイルの上に再生アイコンを重ねる。クリックで拡大プレビュー。
/// </summary>
public sealed class MediaGrid : Grid
{
    private const double Gap = 4;

    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items),
        typeof(object),
        typeof(MediaGrid),
        new PropertyMetadata(null, (d, _) => ((MediaGrid)d).Rebuild()));

    public static readonly DependencyProperty TileHeightProperty = DependencyProperty.Register(
        nameof(TileHeight),
        typeof(double),
        typeof(MediaGrid),
        new PropertyMetadata(120.0, (d, _) => ((MediaGrid)d).Rebuild()));

    public MediaGrid()
    {
        ColumnSpacing = Gap;
        RowSpacing = Gap;
        Visibility = Visibility.Collapsed;
    }

    /// <summary><see cref="IReadOnlyList{MediaItemModel}"/>。空なら非表示。</summary>
    public object? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>1 行の高さ。1 枚のときは 2 倍弱、2 枚のときは 1.5 倍に広げる。</summary>
    public double TileHeight
    {
        get => (double)GetValue(TileHeightProperty);
        set => SetValue(TileHeightProperty, value);
    }

    private void Rebuild()
    {
        Children.Clear();
        RowDefinitions.Clear();
        ColumnDefinitions.Clear();

        var items = (Items as IEnumerable<MediaItemModel>)?.Take(4).ToList() ?? [];
        if (items.Count == 0)
        {
            Visibility = Visibility.Collapsed;
            Height = 0;
            return;
        }

        Visibility = Visibility.Visible;
        var tile = TileHeight;
        switch (items.Count)
        {
            case 1:
                Height = tile * 1.8;
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                Place(items[0], 0, 0, 1);
                break;
            case 2:
                Height = tile * 1.5;
                AddTwoColumns();
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                Place(items[0], 0, 0, 1);
                Place(items[1], 0, 1, 1);
                break;
            case 3:
                Height = tile * 2 + Gap;
                AddTwoColumns();
                AddTwoRows();
                Place(items[0], 0, 0, 2);
                Place(items[1], 0, 1, 1);
                Place(items[2], 1, 1, 1);
                break;
            default:
                Height = tile * 2 + Gap;
                AddTwoColumns();
                AddTwoRows();
                Place(items[0], 0, 0, 1);
                Place(items[1], 0, 1, 1);
                Place(items[2], 1, 0, 1);
                Place(items[3], 1, 1, 1);
                break;
        }
    }

    private void AddTwoColumns()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    }

    private void AddTwoRows()
    {
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
    }

    private void Place(MediaItemModel item, int row, int column, int rowSpan)
    {
        var tile = CreateTile(item);
        SetRow(tile, row);
        SetColumn(tile, column);
        SetRowSpan(tile, rowSpan);
        Children.Add(tile);
    }

    private FrameworkElement CreateTile(MediaItemModel item)
    {
        var image = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Source = item.ThumbnailImage,
        };

        var content = new Grid();
        content.Children.Add(image);

        if (item.IsVideo)
        {
            var overlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x90, 0, 0, 0)),
                CornerRadius = new CornerRadius(24),
                Width = 48,
                Height = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Child = new FontIcon
                {
                    Glyph = "\uE768",
                    FontSize = 20,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            content.Children.Add(overlay);

            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(6),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = item.IsGif ? "GIF" : "動画",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Colors.White),
                },
            };
            content.Children.Add(badge);
        }

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0x80, 0x80, 0x80)),
            Child = content,
        };
        ToolTipService.SetToolTip(border, item.IsVideo ? (item.IsGif ? "GIF を再生" : "動画を再生") : "画像を拡大");
        border.Tapped += async (sender, e) =>
        {
            e.Handled = true;
            await MediaPreviewDialog.ShowAsync(this, item);
        };

        return border;
    }
}
