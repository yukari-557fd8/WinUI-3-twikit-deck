using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinUI3TwikitDeck.Helpers;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.Controls;

/// <summary>
/// 画像グリッド。1 枚: フル幅 / 2 枚: 左右 50% / 3 枚: 左 50% + 右上下 / 4 枚: 2x2。
/// 高さはカラム幅に合わせて決める（<see cref="UpdateHeight"/>）。
/// 動画・GIF にはサムネイルの上に再生アイコンを重ねる。クリックで拡大プレビュー。
/// 表示倍率が変わったら、新しい倍率でデコードし直したサムネイルに差し替える。
/// </summary>
public sealed class MediaGrid : Grid
{
    private const double Gap = 4;

    private int _count;
    private double _lastWidth;
    private bool _subscribed;

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
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary><see cref="IReadOnlyList{MediaItemModel}"/>。空なら非表示。</summary>
    public object? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>高さの基準（DIP）。実際の高さはカラム幅に合わせて <see cref="UpdateHeight"/> で決める。</summary>
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
        _count = items.Count;
        if (items.Count == 0)
        {
            Visibility = Visibility.Collapsed;
            Height = 0;
            return;
        }

        Visibility = Visibility.Visible;
        switch (items.Count)
        {
            case 1:
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                Place(items[0], 0, 0, 1);
                break;
            case 2:
                AddTwoColumns();
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                Place(items[0], 0, 0, 1);
                Place(items[1], 0, 1, 1);
                break;
            case 3:
                AddTwoColumns();
                AddTwoRows();
                Place(items[0], 0, 0, 2);
                Place(items[1], 0, 1, 1);
                Place(items[2], 1, 1, 1);
                break;
            default:
                AddTwoColumns();
                AddTwoRows();
                Place(items[0], 0, 0, 1);
                Place(items[1], 0, 1, 1);
                Place(items[2], 1, 0, 1);
                Place(items[3], 1, 1, 1);
                break;
        }

        UpdateHeight();
    }

    /// <summary>
    /// 高さをカラム幅に合わせて決める（幅の広いカラムで画像が横長につぶれないように）。
    /// 幅がまだ分からない（初回の計測前）ときは <see cref="TileHeight"/> から固定の比率で決め、計測後に取り直す。
    /// </summary>
    private void UpdateHeight()
    {
        var tile = TileHeight;
        var width = ActualWidth;
        switch (_count)
        {
            case 0:
                Height = 0;
                break;
            case 1:
                // 16:9 を基準に伸縮
                Height = width > 0 ? Math.Clamp(width * 9 / 16, tile * 1.2, tile * 3) : tile * 1.8;
                break;
            case 2:
                // 左右 2 枚: 各タイルを正方形に近く
                Height = width > 0 ? Math.Clamp((width - Gap) / 2, tile, tile * 2.5) : tile * 1.5;
                break;
            default:
                // 2 行: 各タイルを 4:3 に近く
                var row = width > 0 ? Math.Clamp((width - Gap) / 2 * 0.75, tile * 0.8, tile * 1.6) : tile;
                Height = row * 2 + Gap;
                break;
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 高さを変えると再び SizeChanged が来るので、幅が変わったときだけ計算し直す
        if (Math.Abs(e.NewSize.Width - _lastWidth) < 0.5)
        {
            return;
        }

        _lastWidth = e.NewSize.Width;
        UpdateHeight();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed)
        {
            DisplayScale.Changed += OnDisplayScaleChanged;
            _subscribed = true;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribed)
        {
            DisplayScale.Changed -= OnDisplayScaleChanged;
            _subscribed = false;
        }
    }

    /// <summary>拡大率が変わった。ImageCache は倍率ごとに別エントリなので、作り直せば新しい倍率のサムネイルになる。</summary>
    private void OnDisplayScaleChanged(double scale) => Rebuild();

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
