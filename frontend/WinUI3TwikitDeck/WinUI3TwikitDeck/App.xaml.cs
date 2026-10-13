using System;
using System.IO;
using Microsoft.UI.Xaml;
using WinUI3TwikitDeck.Bridge;
using WinUI3TwikitDeck.Helpers;
using WinUI3TwikitDeck.Services;
using WinUI3TwikitDeck.ViewModels;

namespace WinUI3TwikitDeck;

public partial class App : Application
{
    private Window? _window;

    public static Window? MainWindowInstance { get; private set; }

    public App()
    {
        InitializeComponent();

        UnhandledException += (_, e) =>
        {
            CrashLog.Write("UnhandledException", $"{e.Message}\n{e.Exception}");
            AppNotifications.Error($"予期しないエラー: {e.Message}");
            e.Handled = true;
        };
        DebugSettings.XamlResourceReferenceFailed += (_, e)
            => CrashLog.Write("XamlResourceReferenceFailed", e.Message);
        DebugSettings.BindingFailed += (_, e)
            => CrashLog.Write("BindingFailed", e.Message);
        DebugSettings.IsXamlResourceReferenceTracingEnabled = true;
        DebugSettings.IsBindingTracingEnabled = true;
        AppDomain.CurrentDomain.UnhandledException += (_, e)
            => CrashLog.Write("AppDomain.UnhandledException", e.ExceptionObject?.ToString() ?? "(null)");
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashLog.Write("UnobservedTaskException", e.Exception.ToString());
            e.SetObserved();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var cookiesPath = RepositoryPaths.CookiesPath;
        var hasCookies = File.Exists(cookiesPath) && CookiesFile.TryRead(cookiesPath, out var auth, out var ct0)
                         && !string.IsNullOrWhiteSpace(auth);

        ITwikitClient client = hasCookies ? new RealTwikitClient() : new MockTwikitClient();
        ITweetActionService actions = hasCookies ? new RealTweetActionService() : new MockTweetActionService();

        var timelineService = new TwikitTimelineService(client);
        var viewModel = new MainViewModel(timelineService);
        AppServices.Configure(timelineService, actions, viewModel);

        _window = new MainWindow();
        MainWindowInstance = _window;

        if (_window.Content is FrameworkElement root)
        {
            root.DataContext = viewModel;

            root.Loaded += async (_, _) =>
            {
                // Cookie が未設定の場合は初回起動時に設定ダイアログを案内
                if (!hasCookies && root.XamlRoot is not null)
                {
                    var dialog = new Controls.SettingsDialog
                    {
                        XamlRoot = root.XamlRoot
                    };
                    await Controls.DialogHelper.ShowAsync(dialog);
                    AppNotifications.Show("Cookie が未設定のため、サンプルデータを表示しています。設定後にアプリを再起動してください。", AppNotificationSeverity.Warning);
                }
            };
        }

        _window.Activate();

        if (hasCookies)
        {
            _ = StartBridgeAsync();
        }
        else
        {
            TwikitBridge.ReportOffline("Twikit: Cookie 未設定 ❌（サンプルデータを表示中）");
        }

        _ = viewModel.InitializeAsync();
    }

    private static async System.Threading.Tasks.Task StartBridgeAsync()
    {
        try
        {
            var status = await TwikitBridge.StartAsync();
            if (!status.Ok)
            {
                AppNotifications.Error(status.Message);
            }
        }
        catch (Exception ex)
        {
            AppNotifications.Error($"Twikit の初期化に失敗しました: {ex.Message}");
        }
    }
}
