using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WinUI3TwikitDeck.Services;

/// <summary>Cookie 未設定時のダミー。少し待って成功を返す。</summary>
public sealed class MockTweetActionService : ITweetActionService
{
    public async Task<ActionOutcome> SetLikeAsync(string tweetId, bool like)
    {
        await Task.Delay(400).ConfigureAwait(false);
        return ActionOutcome.Succeeded;
    }

    public async Task<ActionOutcome> SetRetweetAsync(string tweetId, bool retweet)
    {
        await Task.Delay(400).ConfigureAwait(false);
        return ActionOutcome.Succeeded;
    }

    public async Task<ComposeResult> PostAsync(ComposeRequest request, IProgress<ComposeProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var items = new List<ComposeMediaProgress>();
        foreach (var path in request.MediaPaths)
        {
            items.Add(new ComposeMediaProgress(path, "waiting", 0, null));
        }

        progress?.Report(new ComposeProgress("running", "投稿処理中...", items));
        for (var i = 0; i < items.Count; i++)
        {
            for (var percent = 0; percent <= 100; percent += 25)
            {
                await Task.Delay(120, cancellationToken).ConfigureAwait(false);
                items[i] = new ComposeMediaProgress(items[i].Path, percent < 100 ? "uploading" : "done", percent, null);
                progress?.Report(new ComposeProgress("running", "投稿処理中...", new List<ComposeMediaProgress>(items)));
            }
        }

        await Task.Delay(400, cancellationToken).ConfigureAwait(false);
        var id = DateTime.UtcNow.Ticks.ToString();
        progress?.Report(new ComposeProgress("succeeded", "ツイートしました（モック）", items));
        return new ComposeResult(true, id, "ツイートしました（モック: Cookie 未設定のため送信していません）");
    }
}
