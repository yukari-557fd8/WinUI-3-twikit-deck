using System;

namespace WinUI3TwikitDeck.Helpers;

/// <summary>
/// 現在の表示倍率（96 DPI = 1.0）。XAML のレイアウトは DIP なので拡大率に合わせて自動で拡縮されるが、
/// <c>BitmapImage.DecodePixelWidth</c> は物理ピクセルなので、デコードサイズはこの倍率を掛けて決める。
/// MainWindow が <c>XamlRoot.RasterizationScale</c> から更新する（拡大率の違うモニターへ移動したときも含む）。
/// UI スレッドからのみ使う。
/// </summary>
public static class DisplayScale
{
    private static double _current = 1.0;

    public static double Current => _current;

    /// <summary>倍率が変わった（拡大率の違うモニターへ移動した、OS の拡大率を変えた）。</summary>
    public static event Action<double>? Changed;

    public static void Update(double scale)
    {
        if (!(scale > 0) || double.IsInfinity(scale) || Math.Abs(scale - _current) < 0.001)
        {
            return;
        }

        _current = scale;
        Changed?.Invoke(scale);
    }

    /// <summary>
    /// DIP を現在の倍率で物理ピクセルに直す。
    /// <paramref name="max"/> は元画像より大きくデコードして無駄にメモリを使わないための上限。
    /// </summary>
    public static int ToPhysicalPixels(double logical, int max = int.MaxValue)
    {
        var pixels = (int)Math.Ceiling(logical * _current);
        return Math.Clamp(pixels, 1, Math.Max(1, max));
    }
}
