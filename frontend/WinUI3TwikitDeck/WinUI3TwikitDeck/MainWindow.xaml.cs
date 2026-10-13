using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.UI;
using WinUI3TwikitDeck.Bridge;
using WinUI3TwikitDeck.Controls;
using WinUI3TwikitDeck.Services;
using WinUI3TwikitDeck.ViewModels;

namespace WinUI3TwikitDeck;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherQueueTimer _notificationTimer;

    public MainWindow()
    {
        InitializeComponent();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "TweetDeckIcon48.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }

        ApplyTitleBarTheme();
        if (Content is FrameworkElement root)
        {
            root.ActualThemeChanged += (_, _) => ApplyTitleBarTheme();
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

    private Microsoft.UI.Xaml.Media.Brush? _statusNormalBrush;

    private static readonly Microsoft.UI.Xaml.Media.Brush StatusWarningBrush =
        new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 0xF7, 0x63, 0x0C));

    private void ShowBridgeStatus(BridgeStatus status)
    {
        _statusNormalBrush ??= TwikitStatusText.Foreground;
        TwikitStatusText.Text = status.Message;
        TwikitStatusText.Foreground = status.Ok ? _statusNormalBrush : StatusWarningBrush;
    }

    private MainViewModel? ViewModel => (Content as FrameworkElement)?.DataContext as MainViewModel;

    private XamlRoot? Root => (Content as FrameworkElement)?.XamlRoot;

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
        if (context.Mode == ComposeMode.Reply)
        {
            context.Target?.NoteReplyPosted();
        }

        _ = RefreshOwnTimelinesSoonAsync();
    }

    /// <summary>投稿が反映されるのを少し待ってからホームカラムを更新する。</summary>
    private async Task RefreshOwnTimelinesSoonAsync()
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
            await Task.WhenAll(targets.Select(c => c.RefreshAsync()));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"投稿後の更新に失敗: {ex.Message}");
        }
    }
}
