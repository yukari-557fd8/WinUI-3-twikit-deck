using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media.Imaging;

namespace WinUI3TwikitDeck.Compose;

/// <summary>投稿に添付するローカルファイル 1 件（WinUI-3-twikit より移植）。</summary>
public sealed class MediaFile : INotifyPropertyChanged
{
    public string FilePath { get; set; } = string.Empty;

    public string FileName =>
        string.IsNullOrWhiteSpace(FilePath) ? string.Empty : Path.GetFileName(FilePath);

    public bool IsVideo => MediaAttachmentHelper.IsVideoExtension(Path.GetExtension(FilePath));

    public bool IsGif => MediaAttachmentHelper.IsGifExtension(Path.GetExtension(FilePath));

    /// <summary>動画または GIF（X では mp4 扱い）。1 ツイートに 1 件のみ。</summary>
    public bool IsExclusive => MediaAttachmentHelper.IsExclusiveMediaExtension(Path.GetExtension(FilePath));

    private BitmapImage? _preview;

    public BitmapImage? Preview
    {
        get => _preview;
        set
        {
            if (_preview == value)
            {
                return;
            }

            _preview = value;
            OnPropertyChanged();
        }
    }

    private string _progressText = string.Empty;

    public string ProgressText
    {
        get => _progressText;
        set
        {
            if (_progressText == value)
            {
                return;
            }

            _progressText = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>phase: pending|waiting|uploading|processing|done|failed</summary>
    public void ApplyJobProgress(string? phase, int percent, string? error)
    {
        var name = string.IsNullOrEmpty(FileName) ? "メディア" : FileName;
        ProgressText = (phase ?? string.Empty).ToLowerInvariant() switch
        {
            "waiting" or "pending" => $"{name}: 待機中",
            "uploading" => $"{name}: アップロード中 ({System.Math.Clamp(percent, 0, 100)}%)",
            "processing" => $"{name}: 処理中",
            "done" => $"{name}: 完了",
            "failed" => string.IsNullOrWhiteSpace(error) ? $"{name}: 失敗" : $"{name}: 失敗 ({error})",
            _ => ProgressText,
        };
    }

    public void ClearProgress() => ProgressText = string.Empty;
}
