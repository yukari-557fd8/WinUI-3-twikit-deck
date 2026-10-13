using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Twikit;

namespace WinUI3TwikitDeck.Bridge
{
    /// <summary>
    /// ツイート検索（WinUI-3-twikit より移植）。
    /// Deck では同じクエリのカラムが複数あり得るため、呼び出しごとに状態を持たない。
    /// cursor が null なら最新ページ、続きは戻り値の next_cursor（<see cref="PageCursorStore{T}"/> のトークン）で取る。
    /// </summary>
    internal static class SearchService
    {
        private static readonly PageCursorStore<Tweet> Pages = new(capacity: 256);

        /// <returns><c>{"tweets": [...], "next_cursor": string?}</c>。失敗時は <c>error</c> を含む。</returns>
        public static async Task<JsonObject> SearchTweetsAsync(string query, int count, string product, string? cursor)
        {
            var results = new JsonArray();
            string? nextCursor = null;

            try
            {
                TwikitSession.Login();
                var client = TwikitSession.Client;
                Debug.WriteLine($"検索取得中... query='{query}' product={product} cursor={(string.IsNullOrEmpty(cursor) ? "なし" : "あり")}");

                var (page, token) = await Pages.FetchAsync(
                    cursor,
                    count,
                    c => client.SearchTweetAsync(query, product, count, c)).ConfigureAwait(false);

                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var tweet in page)
                {
                    if (!seen.Add(tweet.Id))
                    {
                        continue;
                    }

                    results.Add(TweetSerializer.TweetToDict(tweet));
                }

                nextCursor = token;
                Debug.WriteLine($"検索完了: {results.Count} 件 (next_cursor {(token is null ? "なし" : "あり")})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"検索エラー: {ex.Message}");
                return new JsonObject { ["error"] = ex.Message, ["tweets"] = results, ["next_cursor"] = null };
            }

            return new JsonObject { ["tweets"] = results, ["next_cursor"] = nextCursor };
        }
    }
}
