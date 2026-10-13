using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Twikit;

namespace WinUI3TwikitDeck.Bridge
{
    /// <summary>
    /// twikit-dotnet の <see cref="Client"/> と Cookie を一元管理する（WinUI-3-twikit より移植）。
    /// </summary>
    internal static class TwikitSession
    {
        private static readonly object Gate = new();

        /// <summary>
        /// X への API 呼び出しを 1 件ずつ直列化する。
        /// twikit-dotnet の <see cref="Client"/> はスレッド安全ではなく（<c>GetCookies()</c> が CookieContainer を
        /// 列挙している最中に、並行するリクエストの応答が <c>SetCookies</c> で書き換えると
        /// "Collection was modified" になる）、Deck は複数カラムを同時に読み込むため必須。
        /// </summary>
        private static readonly SemaphoreSlim ApiGate = new(1, 1);

        private static Client? _client;
        private static string? _loadedCookiesPath;
        private static DateTime _loadedCookiesWriteTimeUtc;
        private static long _loadedCookiesLength;
        private static string? _screenName;

        /// <summary>共有クライアント。初回アクセス時に作成する。</summary>
        public static Client Client
        {
            get
            {
                lock (Gate)
                {
                    return _client ??= CreateClient();
                }
            }
        }

        private static Client CreateClient() => new("ja-JP");

        /// <summary>API 呼び出しをゲート越しに実行する（Bridge の各サービスはこれを通す）。</summary>
        public static async Task<T> RunAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            await ApiGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await operation().ConfigureAwait(false);
            }
            finally
            {
                ApiGate.Release();
            }
        }

        /// <summary><see cref="RunAsync{T}"/> の戻り値なし版。</summary>
        public static async Task RunAsync(Func<Task> operation, CancellationToken cancellationToken = default)
        {
            await ApiGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await operation().ConfigureAwait(false);
            }
            finally
            {
                ApiGate.Release();
            }
        }

        /// <summary>
        /// 中央集中ログイン関数。<c>data/cookies.json</c> を読み込む。
        /// ファイルが更新されていれば再読込する（設定画面で保存した直後から新しい Cookie が使われる）。
        /// </summary>
        /// <exception cref="BridgeException">Cookie ファイルが無い、または読み込めない。</exception>
        public static void Login()
        {
            var path = RepositoryPaths.CookiesPath;
            if (!File.Exists(path))
            {
                throw new BridgeException("Cookiesファイルが見つかりません");
            }

            var info = new FileInfo(path);
            lock (Gate)
            {
                if (_client is not null
                    && string.Equals(_loadedCookiesPath, path, StringComparison.OrdinalIgnoreCase)
                    && _loadedCookiesWriteTimeUtc == info.LastWriteTimeUtc
                    && _loadedCookiesLength == info.Length)
                {
                    return;
                }

                try
                {
                    var client = _client ??= CreateClient();
                    client.LoadCookies(path);
                    _loadedCookiesPath = path;
                    _loadedCookiesWriteTimeUtc = info.LastWriteTimeUtc;
                    _loadedCookiesLength = info.Length;
                    _screenName = null;
                    Debug.WriteLine("クッキーロード完了（TwikitSession）");
                }
                catch (Exception ex)
                {
                    throw new BridgeException($"ログイン失敗: {ex.Message}", ex);
                }
            }
        }

        /// <summary>クライアントを作り直す（設定画面の「再起動」に相当）。</summary>
        public static void Reset()
        {
            Client? old;
            lock (Gate)
            {
                old = _client;
                _client = null;
                _loadedCookiesPath = null;
                _screenName = null;
            }

            old?.Dispose();
        }

        /// <summary>
        /// ログイン中ユーザーのスクリーンネーム。ユーザー名はコードに書かず、セッションから取る。
        /// </summary>
        public static async Task<string> GetAuthenticatedScreenNameAsync()
        {
            lock (Gate)
            {
                if (_screenName is not null)
                {
                    return _screenName;
                }
            }

            var response = await RunAsync(() => Client.V11.SettingsAsync()).ConfigureAwait(false);
            if (response.Object is null)
            {
                throw new BridgeException("アカウント設定の応答が不正です");
            }

            var screenName = (response.Object.Str("screen_name") ?? string.Empty).Trim().TrimStart('@');
            if (screenName.Length == 0)
            {
                throw new BridgeException("ログイン中ユーザー名を取得できませんでした");
            }

            lock (Gate)
            {
                _screenName = screenName;
            }

            return screenName;
        }

        /// <summary>ログイン中のユーザー（settings の screen_name → UserByScreenName）。</summary>
        public static async Task<User> GetAuthenticatedUserAsync()
        {
            var screenName = await GetAuthenticatedScreenNameAsync().ConfigureAwait(false);
            return await RunAsync(() => Client.GetUserByScreenNameAsync(screenName)).ConfigureAwait(false);
        }
    }

    /// <summary>ブリッジ自身が検出したエラー（Cookie が無いなど）。</summary>
    internal sealed class BridgeException : Exception
    {
        public BridgeException(string message)
            : base(message)
        {
        }

        public BridgeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
