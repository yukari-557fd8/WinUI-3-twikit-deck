using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace WinUI3TwikitDeck.Compose;

/// <summary>添付メディアの選択・クリップボード貼り付け（WinUI-3-twikit より移植）。</summary>
internal static class MediaAttachmentHelper
{
    /// <summary>画像は最大 4 枚。動画は 1 本だけ（X の制限）。</summary>
    public const int MaxMediaCount = 4;

    public static readonly string[] AllowedMediaExtensions =
    [
        ".pjp", ".jfif", ".jpe", ".pjpeg", ".jpeg", ".jpg", ".png", ".webp", ".gif", ".m4v", ".mp4", ".mov",
    ];

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".m4v",
    };

    public static string MediaFileFilterSpec =>
        string.Join(';', AllowedMediaExtensions.Select(ext => $"*{ext}"));

    public static IntPtr GetMainWindowHandle()
    {
        if (App.MainWindowInstance is null)
        {
            return IntPtr.Zero;
        }

        return WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
    }

    public static IReadOnlyList<string> PickMedia(IntPtr hwnd)
    {
        return CommonOpenFileDialog.Show(
            hwnd,
            filters:
            [
                ("メディアファイル", MediaFileFilterSpec),
                ("すべてのファイル", "*.*"),
            ],
            multiSelect: true,
            title: "メディアを選択",
            initialDirectory: Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
    }

    public static bool IsAllowedMediaExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return AllowedMediaExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsVideoExtension(string? extension)
        => !string.IsNullOrWhiteSpace(extension) && VideoExtensions.Contains(extension);

    public static bool IsGifExtension(string? extension)
        => string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 動画または GIF。X では GIF も mp4 に変換される動画扱い（<c>tweet_gif</c>）で、
    /// 1 ツイートに 1 件のみ、画像との混在は不可。
    /// </summary>
    public static bool IsExclusiveMediaExtension(string? extension)
        => IsVideoExtension(extension) || IsGifExtension(extension);

    /// <summary>
    /// 追加できた件数を返す。動画・GIF は単独で 1 件のみ、画像との混在は不可。
    /// 入らなかった理由は <paramref name="rejectedReason"/>。
    /// </summary>
    public static int AddFromPaths(ObservableCollection<MediaFile> selectedFiles, IEnumerable<string> paths, out string? rejectedReason)
    {
        rejectedReason = null;
        var added = 0;
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var extension = Path.GetExtension(path);
            if (!IsAllowedMediaExtension(extension))
            {
                rejectedReason = $"対応していないファイルです: {Path.GetFileName(path)}";
                continue;
            }

            var isExclusive = IsExclusiveMediaExtension(extension);
            if (isExclusive && selectedFiles.Count > 0)
            {
                rejectedReason = "動画・GIF は単独で 1 件だけ添付できます";
                continue;
            }

            if (!isExclusive && selectedFiles.Any(f => f.IsExclusive))
            {
                rejectedReason = "動画・GIF と画像は同時に添付できません";
                continue;
            }

            if (selectedFiles.Count >= MaxMediaCount)
            {
                rejectedReason = $"添付できるのは {MaxMediaCount} 件までです";
                break;
            }

            if (selectedFiles.Any(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var media = new MediaFile { FilePath = path };
            if (!IsVideoExtension(extension))
            {
                try
                {
                    media.Preview = new BitmapImage(new Uri(path)) { DecodePixelWidth = 240 };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Preview failed: {ex.Message}");
                }
            }

            selectedFiles.Add(media);
            added++;
        }

        return added;
    }

    public static async Task<bool> TryAddFromClipboardAsync(ObservableCollection<MediaFile> selectedFiles)
    {
        if (selectedFiles.Count >= MaxMediaCount)
        {
            return false;
        }

        var content = Clipboard.GetContent();
        if (content is null)
        {
            return false;
        }

        if (content.Contains(StandardDataFormats.Bitmap))
        {
            var path = await SaveClipboardBitmapToTempPngAsync(content);
            if (path is not null)
            {
                return AddFromPaths(selectedFiles, [path], out _) > 0;
            }
        }

        if (content.Contains(StandardDataFormats.StorageItems))
        {
            var items = await content.GetStorageItemsAsync();
            var paths = items
                .OfType<StorageFile>()
                .Where(f => IsAllowedMediaExtension(f.FileType))
                .Select(f => f.Path)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (paths.Count > 0)
            {
                return AddFromPaths(selectedFiles, paths, out _) > 0;
            }
        }

        return false;
    }

    private static async Task<string?> SaveClipboardBitmapToTempPngAsync(DataPackageView content)
    {
        var streamRef = await content.GetBitmapAsync();
        using var input = await streamRef.OpenReadAsync();

        var decoder = await BitmapDecoder.CreateAsync(input);
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        var fileName = $"media_paste_{Guid.NewGuid():N}.png";
        var tempFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetTempPath());
        var file = await tempFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

        using (var output = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);

            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                softwareBitmap.BitmapAlphaMode == BitmapAlphaMode.Straight)
            {
                softwareBitmap = SoftwareBitmap.Convert(
                    softwareBitmap,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);
            }

            encoder.SetSoftwareBitmap(softwareBitmap);
            await encoder.FlushAsync();
        }

        return file.Path;
    }
}
