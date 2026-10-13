using System;
using System.Threading;
using System.Threading.Tasks;
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
            return await dialog.ShowAsync();
        }
        finally
        {
            Volatile.Write(ref _open, 0);
        }
    }
}
