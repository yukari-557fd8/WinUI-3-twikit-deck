using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Twikit;

namespace WinUI3TwikitDeck.Bridge
{
    /// <summary>ツイート投稿・返信・引用（WinUI-3-twikit より移植。返信先の指定を追加）。</summary>
    internal static class TweetPoster
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pjp", ".jfif", ".jpe", ".pjpeg", ".jpeg", ".jpg", ".png", ".webp", ".gif", ".m4v", ".mp4", ".mov",
        };

        private static string ValidateMediaPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("メディアのパスが空です");
            }

            var trimmed = path.Trim();
            if (!Path.IsPathRooted(trimmed))
            {
                throw new ArgumentException($"メディアのパスは絶対パスにしてください: {path}");
            }

            var normalized = Path.GetFullPath(trimmed);
            if (!File.Exists(normalized))
            {
                throw new ArgumentException($"メディアファイルがありません: {normalized}");
            }

            var ext = Path.GetExtension(normalized).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                throw new ArgumentException($"対応していない拡張子です: {ext}");
            }

            return normalized;
        }

        /// <summary>X / twikit のメディアエラーを読みやすい日本語にする。</summary>
        private static string FriendlyInvalidMediaMessage(Exception exc)
        {
            var raw = exc.Message ?? string.Empty;
            if (Regex.IsMatch(raw, "Duration too long", RegexOptions.IgnoreCase))
            {
                var maxMatch = Regex.Match(raw, @"maximum:\s*([^\s,]+)", RegexOptions.IgnoreCase);
                var actualMatch = Regex.Match(raw, @"actual:\s*([^\s,(]+)", RegexOptions.IgnoreCase);
                var maxText = maxMatch.Success ? maxMatch.Groups[1].Value : "不明";
                var actualText = actualMatch.Success ? actualMatch.Groups[1].Value : "不明";
                return "動画の長さがアカウントの上限を超えています。"
                       + $" 上限={maxText} / 実際={actualText}。"
                       + " 短い動画に切り出すか、X Premium 等で長い動画が許可されているアカウントを使ってください。"
                       + $" (詳細: {raw})";
            }

            return $"メディアが拒否されました: {raw}";
        }

        /// <summary>パスを検証してジョブを作る（処理はまだ始めない）。</summary>
        public static TweetJob StartTweetJob(string? text, IReadOnlyList<string>? paths, string? replyTo = null, string? quoteTweetId = null)
        {
            var validated = new List<string>();
            if (paths is not null)
            {
                foreach (var raw in paths)
                {
                    validated.Add(ValidateMediaPath(raw));
                }
            }

            return TweetJobStore.Instance.Create(text ?? string.Empty, validated, replyTo, quoteTweetId);
        }

        /// <summary>バックグラウンド処理: 進捗を更新しながらメディアをアップロードし、ツイートを作成する。</summary>
        public static async Task RunTweetJobAsync(string jobId)
        {
            var store = TweetJobStore.Instance;
            var job = store.Get(jobId);
            if (job is null)
            {
                return;
            }

            store.SetState(jobId, "running", message: "投稿処理中...");

            for (var i = 0; i < job.Items.Count; i++)
            {
                store.UpdateItem(jobId, i, phase: "waiting", percent: 0);
            }

            var mediaIds = new List<string>();
            try
            {
                TwikitSession.Login();
                var client = TwikitSession.Client;

                for (var index = 0; index < job.Items.Count; index++)
                {
                    var path = job.Items[index].Path;
                    var itemIndex = index;
                    string mediaId;

                    if (MediaUpload.IsVideoPath(path))
                    {
                        store.UpdateItem(jobId, index, phase: "uploading", percent: 0);
                        mediaId = await MediaUpload.UploadMediaStreamingAsync(
                            client,
                            path,
                            mediaCategory: "tweet_video",
                            isLongVideo: true,
                            waitForCompletion: true,
                            onProgress: (phase, percent) => store.UpdateItem(jobId, itemIndex, phase: phase, percent: percent))
                            .ConfigureAwait(false);
                    }
                    else if (MediaUpload.IsGifPath(path))
                    {
                        // X は GIF を mp4 に変換して animated_gif として扱う。tweet_gif で送り、変換完了（STATUS）を待つ。
                        store.UpdateItem(jobId, index, phase: "uploading", percent: 0);
                        mediaId = await MediaUpload.UploadMediaStreamingAsync(
                            client,
                            path,
                            mediaCategory: "tweet_gif",
                            isLongVideo: false,
                            waitForCompletion: true,
                            onProgress: (phase, percent) => store.UpdateItem(jobId, itemIndex, phase: phase, percent: percent))
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        store.UpdateItem(jobId, index, phase: "uploading", percent: 0);
                        mediaId = await TwikitSession.RunAsync(() => client.UploadMediaAsync(path)).ConfigureAwait(false);
                        store.UpdateItem(jobId, index, phase: "uploading", percent: 100);
                    }

                    store.UpdateItem(jobId, index, phase: "done", percent: 100);
                    mediaIds.Add(mediaId);
                }

                store.SetState(jobId, "running", message: "ツイート作成中...");
                var attachmentUrl = string.IsNullOrEmpty(job.QuoteTweetId) ? null : $"https://x.com/i/status/{job.QuoteTweetId}";
                var tweet = await TwikitSession.RunAsync(() => client.CreateTweetAsync(
                    job.Text ?? string.Empty,
                    mediaIds.Count > 0 ? mediaIds : null,
                    replyTo: string.IsNullOrEmpty(job.ReplyTo) ? null : job.ReplyTo,
                    attachmentUrl: attachmentUrl)).ConfigureAwait(false);
                var message = $"ツイートしました (ID: {tweet.Id})";
                Debug.WriteLine($"投稿完了  ID: {tweet.Id} (メディア: {mediaIds.Count}件, reply_to={job.ReplyTo}, quote={job.QuoteTweetId})");
                store.SetState(jobId, "succeeded", message: message, tweetId: tweet.Id);
            }
            catch (InvalidMediaException e)
            {
                var friendly = FriendlyInvalidMediaMessage(e);
                store.FailUnfinishedItems(jobId, friendly);
                store.SetState(jobId, "failed", message: friendly);
            }
            catch (Exception e)
            {
                store.FailUnfinishedItems(jobId, e.Message);
                store.SetState(jobId, "failed", message: e.Message);
                Debug.WriteLine($"投稿ジョブ失敗 {jobId}: {e}");
            }
        }
    }
}
