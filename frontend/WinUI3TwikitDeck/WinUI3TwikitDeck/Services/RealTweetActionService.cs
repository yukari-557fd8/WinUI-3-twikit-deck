using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WinUI3TwikitDeck.Bridge;

namespace WinUI3TwikitDeck.Services;

/// <summary>ActionQueue と TweetPoster で X に書き込む実装。</summary>
public sealed class RealTweetActionService : ITweetActionService
{
    public async Task<ActionOutcome> SetLikeAsync(string tweetId, bool like)
    {
        if (string.IsNullOrEmpty(tweetId))
        {
            return ActionOutcome.Failed("ツイート ID がありません");
        }

        var result = await ActionQueue.Instance.EnqueueAsync(new ActionJob(like ? "like" : "unlike", tweetId)).ConfigureAwait(false);
        return result.Ok ? ActionOutcome.Succeeded : ActionOutcome.Failed(result.Error ?? "送信に失敗しました");
    }

    public async Task<ActionOutcome> SetRetweetAsync(string tweetId, bool retweet)
    {
        if (string.IsNullOrEmpty(tweetId))
        {
            return ActionOutcome.Failed("ツイート ID がありません");
        }

        var result = await ActionQueue.Instance.EnqueueAsync(new ActionJob(retweet ? "retweet" : "unretweet", tweetId)).ConfigureAwait(false);
        return result.Ok ? ActionOutcome.Succeeded : ActionOutcome.Failed(result.Error ?? "送信に失敗しました");
    }

    public async Task<ComposeResult> PostAsync(ComposeRequest request, IProgress<ComposeProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        TweetJob job;
        try
        {
            job = TweetPoster.StartTweetJob(request.Text, request.MediaPaths, request.ReplyToTweetId, request.QuoteTweetId);
        }
        catch (ArgumentException ex)
        {
            return new ComposeResult(false, null, ex.Message);
        }

        var store = TweetJobStore.Instance;
        void OnChanged(string jobId)
        {
            if (jobId != job.JobId || progress is null)
            {
                return;
            }

            var snapshot = store.Snapshot(jobId);
            if (snapshot is not null)
            {
                progress.Report(ToProgress(snapshot));
            }
        }

        store.JobChanged += OnChanged;
        try
        {
            await Task.Run(() => TweetPoster.RunTweetJobAsync(job.JobId), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            store.JobChanged -= OnChanged;
        }

        var final = store.Snapshot(job.JobId);
        if (final is null)
        {
            return new ComposeResult(false, null, "ジョブの状態を取得できません");
        }

        var state = final["state"]?.GetValue<string>() ?? string.Empty;
        var message = final["message"]?.GetValue<string>() ?? string.Empty;
        var tweetId = final["tweet_id"]?.GetValue<string>();
        return state == "succeeded"
            ? new ComposeResult(true, tweetId, string.IsNullOrEmpty(message) ? "ツイートしました" : message)
            : new ComposeResult(false, null, string.IsNullOrEmpty(message) ? "ツイートに失敗しました" : message);
    }

    private static ComposeProgress ToProgress(JsonObject snapshot)
    {
        var items = new List<ComposeMediaProgress>();
        if (snapshot["items"] is JsonArray array)
        {
            foreach (var node in array)
            {
                if (node is not JsonObject item)
                {
                    continue;
                }

                items.Add(new ComposeMediaProgress(
                    item["path"]?.GetValue<string>() ?? string.Empty,
                    item["phase"]?.GetValue<string>() ?? "pending",
                    item["percent"]?.GetValue<int>() ?? 0,
                    item["error"]?.GetValue<string>()));
            }
        }

        return new ComposeProgress(
            snapshot["state"]?.GetValue<string>() ?? string.Empty,
            snapshot["message"]?.GetValue<string>() ?? string.Empty,
            items);
    }
}
