using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.System;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.Controls;

/// <summary>画像のフルサイズ表示と動画・GIF の再生（WinUI-3-twikit の MediaPreviewHelper を移植）。</summary>
internal static class MediaPreviewDialog
{
    public static async Task ShowAsync(FrameworkElement host, MediaItemModel item)
    {
        if (host.XamlRoot is null || string.IsNullOrWhiteSpace(item.PreviewUrl))
        {
            return;
        }

        if (item.IsVideo)
        {
            await ShowVideoAsync(host, item);
        }
        else
        {
            await ShowImageAsync(host, item);
        }
    }

    private static Size GetHostSize(FrameworkElement host)
    {
        if (host.XamlRoot is { Size.Width: > 0, Size.Height: > 0 } root)
        {
            return root.Size;
        }

        return new Size(800, 600);
    }

    private static ContentDialog CreateDialog(FrameworkElement host, UIElement content, Size hostSize, string url)
    {
        var dialog = new ContentDialog
        {
            Title = null,
            Content = content,
            CloseButtonText = "閉じる",
            PrimaryButtonText = "ブラウザで開く",
            DefaultButton = ContentDialogButton.Close,
            RequestedTheme = host.ActualTheme,
            FullSizeDesired = true,
            XamlRoot = host.XamlRoot,
        };

        var width = Math.Max(320, hostSize.Width - 48);
        var height = Math.Max(240, hostSize.Height - 48);
        dialog.Resources["ContentDialogMinWidth"] = width;
        dialog.Resources["ContentDialogMaxWidth"] = width;
        dialog.Resources["ContentDialogMinHeight"] = height;
        dialog.Resources["ContentDialogMaxHeight"] = height;
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                await Launcher.LaunchUriAsync(uri);
            }
        };
        return dialog;
    }

    private static async Task ShowImageAsync(FrameworkElement host, MediaItemModel item)
    {
        if (!Uri.TryCreate(item.PreviewUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        var hostSize = GetHostSize(host);
        var bitmap = new BitmapImage
        {
            DecodePixelWidth = Math.Max(1, (int)(hostSize.Width * 0.9)),
            UriSource = uri,
        };

        var image = new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxHeight = hostSize.Height * 0.82,
            MaxWidth = hostSize.Width * 0.9,
        };

        var scroll = new ScrollViewer
        {
            Content = image,
            ZoomMode = ZoomMode.Enabled,
            MinZoomFactor = 1,
            MaxZoomFactor = 4,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        var dialog = CreateDialog(host, scroll, hostSize, item.PreviewUrl);
        await DialogHelper.ShowAsync(dialog);
        image.Source = null;
    }

    private static async Task ShowVideoAsync(FrameworkElement host, MediaItemModel item)
    {
        if (!Uri.TryCreate(item.PreviewUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        var hostSize = GetHostSize(host);
        var player = new MediaPlayerElement
        {
            Source = MediaSource.CreateFromUri(uri),
            AutoPlay = true,
            AreTransportControlsEnabled = !item.IsGif,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxHeight = hostSize.Height * 0.82,
            MaxWidth = hostSize.Width * 0.9,
        };

        var dialog = CreateDialog(host, player, hostSize, item.PreviewUrl);
        dialog.Opened += (_, _) =>
        {
            if (player.MediaPlayer is { } mediaPlayer)
            {
                mediaPlayer.IsLoopingEnabled = item.IsGif;
                mediaPlayer.IsMuted = item.IsGif;
            }
        };

        await DialogHelper.ShowAsync(dialog);

        player.MediaPlayer?.Pause();
        player.Source = null;
    }
}
