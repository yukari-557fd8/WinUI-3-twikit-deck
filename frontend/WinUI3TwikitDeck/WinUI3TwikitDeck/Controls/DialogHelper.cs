using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUI3TwikitDeck.Controls;

/// <summary>
/// ContentDialog は同じ XamlRoot で同時に 1 つしか開けない（2 つ目は例外）。
/// ここを通して開き、開いている間は 2 つ目を無視する。
/// </summary>
internal static class DialogHelper
{
    private static int _open;

    public static bool IsDialogOpen => Volatile.Read(ref _open) != 0;

    public static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        if (Interlocked.CompareExchange(ref _open, 1, 0) != 0)
        {
            return ContentDialogResult.None;
        }

        try
        {
            FitToWindow(dialog);
            return await dialog.ShowAsync();
        }
        finally
        {
            Volatile.Write(ref _open, 0);
        }
    }

    /// <summary>ContentDialog の中身の左右の余白（既定テンプレートの Padding）とウィンドウ端の余裕。</summary>
    private const double DialogHorizontalChrome = 24 * 2 + 16;

    /// <summary>
    /// ダイアログの中身がウィンドウより広いときに縮める。
    /// 各ダイアログの中身には固定幅・最小幅（400〜480 DIP）があり、低解像度や高い拡大率でウィンドウが
    /// それより狭いと ContentDialog ごと切れてしまうため、ウィンドウの幅に収まる値に丸める。
    /// </summary>
    private static void FitToWindow(ContentDialog dialog)
    {
        if (dialog.XamlRoot is not { Size.Width: > 0 } root || dialog.Content is not FrameworkElement content)
        {
            return;
        }

        var available = root.Size.Width - DialogHorizontalChrome;
        if (available <= 0)
        {
            return;
        }

        if (!double.IsNaN(content.Width) && content.Width > available)
        {
            content.Width = double.NaN;
        }

        if (content.MinWidth > available)
        {
            content.MinWidth = available;
        }

        content.MaxWidth = Math.Min(content.MaxWidth, available);
    }
}
