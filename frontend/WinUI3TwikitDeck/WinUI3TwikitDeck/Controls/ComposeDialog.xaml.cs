using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinUI3TwikitDeck.Compose;
using WinUI3TwikitDeck.Services;

namespace WinUI3TwikitDeck.Controls;

/// <summary>新規ツイート・返信・引用の投稿ダイアログ。</summary>
public sealed partial class ComposeDialog : ContentDialog
{
    private const int CharacterLimit = 280;

    private readonly ComposeContext _context;
    private readonly Microsoft.UI.Xaml.Media.Brush _normalCountBrush;
    private readonly Microsoft.UI.Xaml.Media.Brush _overLimitBrush;
    private bool _posting;

    public ComposeDialog(ComposeContext context)
    {
        InitializeComponent();
        _context = context;
        _normalCountBrush = CountText.Foreground;
        _overLimitBrush = StatusText.Foreground;

        Title = context.Mode switch
        {
            ComposeMode.Reply => "返信",
            ComposeMode.Quote => "引用ツイート",
            _ => "ツイート",
        };
        PrimaryButtonText = context.Mode switch
        {
            ComposeMode.Reply => "返信する",
            ComposeMode.Quote => "引用ツイートする",
            _ => "ツイートする",
        };

        if (context.Target is { } target && context.Mode != ComposeMode.New)
        {
            TargetPanel.Visibility = Visibility.Visible;
            TargetLabel.Text = context.Mode == ComposeMode.Reply ? $"返信先: {target.UserScreenName}" : $"引用元: {target.UserScreenName}";
            TargetText.Text = target.Summary;
        }

        InputBox.Text = context.InitialText ?? string.Empty;
        SelectedFiles.CollectionChanged += (_, _) => UpdateMediaInfo();
        PrimaryButtonClick += OnPrimaryButtonClick;
        Closing += OnClosing;
        Opened += (_, _) =>
        {
            InputBox.Focus(FocusState.Programmatic);
            InputBox.SelectionStart = InputBox.Text.Length;
        };
        UpdateCount();
        UpdateMediaInfo();
    }

    public ObservableCollection<MediaFile> SelectedFiles { get; } = [];

    /// <summary>投稿に成功したときの結果。キャンセル・失敗なら null。</summary>
    public ComposeResult? Result { get; private set; }

    private bool CanPost => !_posting && (!string.IsNullOrWhiteSpace(InputBox.Text) || SelectedFiles.Count > 0);

    private void UpdateCount()
    {
        var length = InputBox.Text?.Length ?? 0;
        CountText.Text = $"{length} / {CharacterLimit}";
        CountText.Foreground = length > CharacterLimit ? _overLimitBrush : _normalCountBrush;
        IsPrimaryButtonEnabled = CanPost;
    }

    private void UpdateMediaInfo()
    {
        MediaStrip.Visibility = SelectedFiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (SelectedFiles.Count == 0)
        {
            MediaSummary.Text = string.Empty;
        }
        else
        {
            long totalBytes = 0;
            foreach (var media in SelectedFiles)
            {
                try
                {
                    totalBytes += new FileInfo(media.FilePath).Length;
                }
                catch (Exception)
                {
                }
            }

            MediaSummary.Text = $"{SelectedFiles.Count} 件のメディア ({FormatFileSize(totalBytes)})";
        }

        AddMediaButton.IsEnabled = !_posting
                                   && SelectedFiles.Count < MediaAttachmentHelper.MaxMediaCount
                                   && !SelectedFiles.Any(f => f.IsExclusive);
        IsPrimaryButtonEnabled = CanPost;
    }

    private static string FormatFileSize(long bytes)
    {
        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;

        return bytes switch
        {
            < kb => $"{bytes} B",
            < mb => $"{(double)bytes / kb:F1} KB",
            < gb => $"{(double)bytes / mb:F1} MB",
            _ => $"{(double)bytes / gb:F2} GB",
        };
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateCount();

    private async void InputBox_Paste(object sender, TextControlPasteEventArgs e)
    {
        try
        {
            if (await MediaAttachmentHelper.TryAddFromClipboardAsync(SelectedFiles))
            {
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Paste media failed: {ex.Message}");
        }
    }

    private async void InputBox_CtrlEnterInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (!CanPost)
        {
            return;
        }

        if (await PostAsync())
        {
            Hide();
        }
    }

    private void OnAddMediaClick(object sender, RoutedEventArgs e)
    {
        var paths = MediaAttachmentHelper.PickMedia(MediaAttachmentHelper.GetMainWindowHandle());
        MediaAttachmentHelper.AddFromPaths(SelectedFiles, paths, out var rejected);
        StatusText.Text = rejected ?? string.Empty;
    }

    private void RemoveMedia_Click(object sender, RoutedEventArgs e)
    {
        if (_posting)
        {
            return;
        }

        if (sender is Button { Tag: MediaFile media })
        {
            SelectedFiles.Remove(media);
        }
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            if (!await PostAsync())
            {
                args.Cancel = true;
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (_posting)
        {
            args.Cancel = true;
        }
    }

    private async Task<bool> PostAsync()
    {
        if (!CanPost)
        {
            return false;
        }

        var text = InputBox.Text.Replace("\r\n", "\n").Replace('\r', '\n');
        var paths = SelectedFiles
            .Select(m => m.FilePath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        var request = new ComposeRequest(
            text,
            paths,
            _context.Mode == ComposeMode.Reply ? _context.Target?.Id : null,
            _context.Mode == ComposeMode.Quote ? _context.Target?.Id : null);

        SetPosting(true);
        StatusText.Text = string.Empty;
        foreach (var media in SelectedFiles)
        {
            media.ApplyJobProgress("waiting", 0, null);
        }

        var progress = new Progress<ComposeProgress>(ApplyProgress);
        try
        {
            var result = await AppServices.Actions.PostAsync(request, progress);
            if (result.Ok)
            {
                Result = result;
                return true;
            }

            StatusText.Text = result.Message;
            return false;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"ツイートに失敗しました: {ex.Message}";
            return false;
        }
        finally
        {
            SetPosting(false);
        }
    }

    private void ApplyProgress(ComposeProgress progress)
    {
        var byPath = progress.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Path))
            .GroupBy(i => i.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < SelectedFiles.Count; i++)
        {
            var media = SelectedFiles[i];
            if (byPath.TryGetValue(media.FilePath, out var item))
            {
                media.ApplyJobProgress(item.Phase, item.Percent, item.Error);
            }
            else if (i < progress.Items.Count)
            {
                var fallback = progress.Items[i];
                media.ApplyJobProgress(fallback.Phase, fallback.Percent, fallback.Error);
            }
        }

        if (progress.State == "running" && !string.IsNullOrEmpty(progress.Message))
        {
            MediaSummary.Text = progress.Message;
        }
    }

    private void SetPosting(bool posting)
    {
        _posting = posting;
        IsPrimaryButtonEnabled = CanPost;
        IsSecondaryButtonEnabled = !posting;
        InputBox.IsReadOnly = posting;
        AddMediaButton.IsEnabled = !posting;
        PostingRing.IsActive = posting;
        PostingRing.Visibility = posting ? Visibility.Visible : Visibility.Collapsed;
        if (!posting)
        {
            UpdateMediaInfo();
        }
    }
}
