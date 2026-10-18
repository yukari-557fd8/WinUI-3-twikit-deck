using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.UI;
using WinUI3TwikitDeck.Bridge;
using WinUI3TwikitDeck.Compose;
using WinUI3TwikitDeck.Controls;
using WinUI3TwikitDeck.Helpers;
using WinUI3TwikitDeck.Services;
using WinUI3TwikitDeck.ViewModels;

namespace WinUI3TwikitDeck;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherQueueTimer _notificationTimer;
    private readonly WindowPlacementManager _placement;
    private bool _displayScaleTracked;

    public MainWindow()
    {
        InitializeComponent();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "TweetDeckIcon48.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }

        // ウィンドウの位置・サイズ: 保存があれば復元、無ければ表示するモニターの作業領域に合わせる（Activate の前に行う）
        _placement = new WindowPlacementManager(this);
        _placement.Restore();

        ApplyTitleBarTheme();
        if (Content is FrameworkElement root)
        {
            root.ActualThemeChanged += (_, _) => ApplyTitleBarTheme();
            root.Loaded += (_, _) => TrackDisplayScale(root);
            root.DataContextChanged += (_, _) => PushViewportWidth();
        }

        _notificationTimer = DispatcherQueue.CreateTimer();
        _notificationTimer.Interval = TimeSpan.FromSeconds(6);
        _notificationTimer.IsRepeating = false;
        _notificationTimer.Tick += (_, _) => NotificationBar.IsOpen = false;

        // 時計（1 秒ごと）
        UpdateClock();
        _clockTimer = DispatcherQueue.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.IsRepeating = true;
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();

        // Twikit の状態
        if (TwikitBridge.CurrentStatus is { } current)
        {
            ShowBridgeStatus(current);
        }

        TwikitBridge.StatusChanged += OnBridgeStatusChanged;
        ToolTipService.SetToolTip(TwikitStatusText, $"cookies.json: {RepositoryPaths.CookiesPath}");

        AppNotifications.Posted += OnNotificationPosted;
        AppServices.OpenCompose = ShowComposeAsync;
        Closed += (_, _) =>
        {
            _clockTimer.Stop();
            TwikitBridge.StatusChanged -= OnBridgeStatusChanged;
            AppNotifications.Posted -= OnNotificationPosted;
            if (AppServices.OpenCompose == ShowComposeAsync)
            {
                AppServices.OpenCompose = null;
            }
        };
    }

    private readonly DispatcherQueueTimer _clockTimer;

    private void UpdateClock()
        => ClockText.Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

    private void OnBridgeStatusChanged(BridgeStatus status)
    {
        if (!DispatcherQueue.TryEnqueue(() => ShowBridgeStatus(status)))
        {
            ShowBridgeStatus(status);
        }
    }

    private void ShowBridgeStatus(BridgeStatus status)
    {
        TwikitStatusText.Text = status.Message;

        // Foreground を直接代入するとテーマ参照が切れるので、ThemeResource を持つ Style で切り替える
        var styleKey = status.Ok ? "StatusTextStyle" : "StatusTextWarningStyle";
        if (RootGrid.Resources.TryGetValue(styleKey, out var style) && style is Style textStyle)
        {
            TwikitStatusText.Style = textStyle;
        }
    }

    private MainViewModel? ViewModel => (Content as FrameworkElement)?.DataContext as MainViewModel;

    private XamlRoot? Root => (Content as FrameworkElement)?.XamlRoot;

    // ---- 解像度・DPI ----

    /// <summary>
    /// 表示倍率を <see cref="DisplayScale"/> に反映する。XamlRoot.Changed は拡大率の違うモニターへ移動したときや
    /// OS の拡大率を変えたときに発生する。レイアウトは XAML が自動で拡縮するが、画像のデコードサイズはこれに追従させる。
    /// </summary>
    private void TrackDisplayScale(FrameworkElement root)
    {
        if (_displayScaleTracked || root.XamlRoot is not { } xamlRoot)
        {
            return;
        }

        _displayScaleTracked = true;
        DisplayScale.Update(xamlRoot.RasterizationScale);
        xamlRoot.Changed += (sender, _) => DisplayScale.Update(sender.RasterizationScale);
    }

    private void DeckScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => PushViewportWidth();

    /// <summary>カラム領域の幅（Padding を除く）を ViewModel に渡す。「カラム幅をウィンドウに合わせる」の配分に使う。</summary>
    private void PushViewportWidth()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var padding = DeckScrollViewer.Padding;
        viewModel.ViewportWidth = Math.Max(0, DeckScrollViewer.ActualWidth - padding.Left - padding.Right);
    }

    private void ViewMenu_Opening(object sender, object e)
        => FitColumnsItem.IsChecked = ViewModel?.FitColumnsToWindow ?? false;

    private void FitColumnsItem_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.FitColumnsToWindow = !viewModel.FitColumnsToWindow;
        FitColumnsItem.IsChecked = viewModel.FitColumnsToWindow;
    }

    private void ApplyTitleBarTheme()
    {
        var isLight = Content is FrameworkElement { ActualTheme: ElementTheme.Light };
        var background = isLight
            ? Color.FromArgb(255, 243, 243, 243)
            : Color.FromArgb(255, 32, 32, 32);
        var buttonForeground = isLight
            ? Color.FromArgb(255, 0, 0, 0)
            : Color.FromArgb(255, 255, 255, 255);
        var titleBar = AppWindow.TitleBar;

        titleBar.BackgroundColor = background;
        titleBar.InactiveBackgroundColor = background;
        titleBar.ButtonBackgroundColor = background;
        titleBar.ButtonInactiveBackgroundColor = background;
        titleBar.ButtonForegroundColor = buttonForeground;
        titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 128, 128, 128);
    }

    // ---- 通知バー ----

    private void OnNotificationPosted(AppNotification notification)
    {
        if (!DispatcherQueue.TryEnqueue(() => ShowNotification(notification)))
        {
            ShowNotification(notification);
        }
    }

    private void ShowNotification(AppNotification notification)
    {
        NotificationBar.Severity = notification.Severity switch
        {
            AppNotificationSeverity.Success => InfoBarSeverity.Success,
            AppNotificationSeverity.Warning => InfoBarSeverity.Warning,
            AppNotificationSeverity.Error => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational,
        };
        NotificationBar.Message = notification.Message;
        NotificationBar.IsOpen = true;
        _notificationTimer.Stop();
        _notificationTimer.Start();
    }

    // ---- コマンドバー ----

    // ---- カラム構成のエクスポート / インポート ----

    private static IReadOnlyList<(string Name, string Spec)> LayoutFileFilters { get; } =
    [
        ("カラム構成 (*.json)", "*.json"),
    ];

    private IntPtr WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);

    private void OnExportLayoutClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        try
        {
            var path = CommonSaveFileDialog.Show(
                WindowHandle,
                LayoutFileFilters,
                defaultFileName: DeckLayoutStore.ExportFileName,
                defaultExtension: "json",
                title: "カラム構成をエクスポート",
                initialDirectory: Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (path is null)
            {
                return;
            }

            DeckLayoutStore.Export(path, viewModel.CreateLayout());
            AppNotifications.Success($"カラム構成をエクスポートしました: {path}");
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
        {
            AppNotifications.Error($"カラム構成をエクスポートできません: {ex.Message}");
        }
    }

    private async void OnImportLayoutClick(object sender, RoutedEventArgs e)
    {
        if (Root is null || ViewModel is not { } viewModel || DialogHelper.IsDialogOpen)
        {
            return;
        }

        string? path;
        try
        {
            path = CommonOpenFileDialog.Show(
                    WindowHandle,
                    [.. LayoutFileFilters, ("すべてのファイル", "*.*")],
                    multiSelect: false,
                    title: "カラム構成をインポート",
                    initialDirectory: Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
                .FirstOrDefault();
        }
        catch (COMException ex)
        {
            AppNotifications.Error($"ファイルを選べません: {ex.Message}");
            return;
        }

        if (path is null)
        {
            return;
        }

        var layout = DeckLayoutStore.Import(path, out var error);
        if (layout is null)
        {
            AppNotifications.Error($"カラム構成をインポートできません: {error}");
            return;
        }

        // 今のカラムは閉じて置き換わる。戻せないので確認する
        var confirm = new ContentDialog
        {
            XamlRoot = Root,
            RequestedTheme = (Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Default,
            Title = "カラム構成をインポート",
            Content = new TextBlock
            {
                Text = $"今開いている {viewModel.Columns.Count} 本のカラムを閉じて、{Path.GetFileName(path)} のカラム構成に置き換えます。",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "置き換える",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await DialogHelper.ShowAsync(confirm) != ContentDialogResult.Primary)
        {
            return;
        }

        var opened = await viewModel.ReplaceLayoutAsync(layout);
        if (opened == 0)
        {
            AppNotifications.Error("カラム構成をインポートできません: 開けるカラムがありません。今のカラムはそのままです。");
            return;
        }

        DeckScrollViewer.ChangeView(0, null, null, disableAnimation: true);
        var skipped = layout.Columns.Count - opened;
        if (skipped > 0)
        {
            AppNotifications.Show($"カラム構成をインポートしました（{opened} 本）。対応していない {skipped} 本は読み飛ばしました。", AppNotificationSeverity.Warning);
        }
        else
        {
            AppNotifications.Success($"カラム構成をインポートしました（{opened} 本）。");
        }
    }

    private async void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        if (Root is null)
        {
            return;
        }

        var dialog = new SettingsDialog { XamlRoot = Root };
        await DialogHelper.ShowAsync(dialog);
    }

    private async void OnComposeClick(object sender, RoutedEventArgs e)
        => await ShowComposeAsync(new ComposeContext(ComposeMode.New));

    private async void OnComposeAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowComposeAsync(new ComposeContext(ComposeMode.New));
    }

    private async void OnRefreshAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel is { } viewModel)
        {
            await viewModel.RefreshAllAsync();
        }
    }

    private async void OnAddSearchClick(object sender, RoutedEventArgs e)
    {
        if (Root is null || ViewModel is not { } viewModel)
        {
            return;
        }

        var dialog = new TextInputDialog(
            "検索カラムを追加",
            "検索語を入力してください。X の検索演算子（from:、OR、-除外 など）も使えます。",
            "例: #WinUI OR twikit")
        {
            XamlRoot = Root,
        };
        var result = await DialogHelper.ShowAsync(dialog);
        if (dialog.WasConfirmed(result))
        {
            await viewModel.AddSearchColumnAsync(dialog.Text);
        }
    }

    private async void OnAddUserClick(object sender, RoutedEventArgs e)
    {
        if (Root is null || ViewModel is not { } viewModel)
        {
            return;
        }

        var dialog = new TextInputDialog(
            "ユーザーカラムを追加",
            "ツイートを表示するユーザーの @スクリーンネームを入力してください。",
            "例: @twitter")
        {
            XamlRoot = Root,
        };
        var result = await DialogHelper.ShowAsync(dialog);
        if (dialog.WasConfirmed(result))
        {
            await viewModel.AddUserColumnAsync(dialog.Text);
        }
    }

    private async void OnAddListClick(object sender, RoutedEventArgs e)
    {
        if (Root is null || ViewModel is not { } viewModel)
        {
            return;
        }

        var dialog = new ListPickerDialog(viewModel.LoadListsAsync) { XamlRoot = Root };
        var result = await DialogHelper.ShowAsync(dialog);
        if (dialog.WasConfirmed(result) && dialog.SelectedList is { } list)
        {
            await viewModel.AddListColumnAsync(list);
        }
    }

    // ---- 投稿 ----

    private async Task ShowComposeAsync(ComposeContext context)
    {
        if (Root is null || DialogHelper.IsDialogOpen)
        {
            return;
        }

        var dialog = new ComposeDialog(context)
        {
            XamlRoot = Root,
            RequestedTheme = (Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Default,
        };
        await DialogHelper.ShowAsync(dialog);

        if (dialog.Result is not { Ok: true } result)
        {
            return;
        }

        AppNotifications.Success(result.Message);
        var replyTarget = context.Mode == ComposeMode.Reply ? context.Target : null;
        replyTarget?.NoteReplyPosted();

        _ = RefreshOwnTimelinesSoonAsync(replyTarget?.Id);
    }

    /// <summary>
    /// 投稿が反映されるのを少し待ってからホームカラムを更新する。
    /// 返信なら、返信先のツイートを表示中のカラム内の会話も取り直す（自分の返信が返信欄に出る）。
    /// </summary>
    private async Task RefreshOwnTimelinesSoonAsync(string? replyToTweetId)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            if (ViewModel is not { } viewModel)
            {
                return;
            }

            var targets = viewModel.Columns
                .Where(c => c is HomeColumnViewModel { TimelineType: Models.HomeTimelineType.Latest })
                .ToList();
            List<ConversationViewModel> conversations = string.IsNullOrEmpty(replyToTweetId)
                ? []
                : viewModel.Columns
                    .Select(c => c.Conversation)
                    .OfType<ConversationViewModel>()
                    .Where(c => c.Contains(replyToTweetId))
                    .ToList();
            await Task.WhenAll(targets.Select(c => c.RefreshAsync()).Concat(conversations.Select(c => c.ReloadAsync())));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"投稿後の更新に失敗: {ex.Message}");
        }
    }
}
