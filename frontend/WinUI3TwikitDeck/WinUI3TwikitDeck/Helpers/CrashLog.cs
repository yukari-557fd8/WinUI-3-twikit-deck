using System;
using System.Diagnostics;
using System.IO;

namespace WinUI3TwikitDeck.Helpers;

/// <summary>
/// XAML のリソース参照失敗・バインド失敗・未処理例外を <c>%LOCALAPPDATA%\WinUI3TwikitDeck\errors.log</c> に残す。
/// パッケージ版では Debug 出力が見えないため、起動直後のクラッシュ調査に使う。
/// </summary>
public static class CrashLog
{
    private static readonly object Gate = new();

    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinUI3TwikitDeck",
        "errors.log");

    public static void Write(string category, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{category}] {message}";
        Debug.WriteLine(line);
        try
        {
            lock (Gate)
            {
                var directory = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(Path, line + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // ログに失敗しても本体の動作には影響させない
        }
    }
}
