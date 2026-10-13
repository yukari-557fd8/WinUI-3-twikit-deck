using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using WinUI3TwikitDeck.Bridge;

namespace WinUI3TwikitDeck.Controls;

public sealed partial class SettingsDialog : ContentDialog
{
    private readonly string _cookiesPath;

    public SettingsDialog()
    {
        InitializeComponent();
        _cookiesPath = RepositoryPaths.CookiesPath;
        LoadCurrentCookies();

        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void LoadCurrentCookies()
    {
        if (CookiesFile.TryRead(_cookiesPath, out var auth, out var ct0))
        {
            AuthTokenBox.Password = auth;
            Ct0Box.Password = ct0;
            StatusTextBlock.Text = $"保存先: {_cookiesPath}";
        }
        else
        {
            StatusTextBlock.Text = $"新規保存先: {_cookiesPath}";
        }
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var auth = AuthTokenBox.Password;
            var ct0 = Ct0Box.Password;

            if (string.IsNullOrWhiteSpace(auth) || string.IsNullOrWhiteSpace(ct0))
            {
                StatusTextBlock.Text = "auth_token と ct0 の両方を入力してください。";
                args.Cancel = true;
                return;
            }

            // 保存先ディレクトリ確認・保存
            CookiesFile.Save(_cookiesPath, auth, ct0);
            Debug.WriteLine($"Cookies saved to {_cookiesPath}");

            // セッションリスタート
            var status = await TwikitBridge.RestartAsync().ConfigureAwait(true);
            if (!status.Ok)
            {
                StatusTextBlock.Text = status.Message;
                args.Cancel = true;
                return;
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"保存エラー: {ex.Message}";
            args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }
}
