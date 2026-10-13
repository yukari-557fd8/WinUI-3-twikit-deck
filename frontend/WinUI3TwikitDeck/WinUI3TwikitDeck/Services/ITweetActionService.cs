using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WinUI3TwikitDeck.Services;

/// <summary>いいね・RT の送信結果。</summary>
public readonly record struct ActionOutcome(bool Ok, string? Error)
{
    public static ActionOutcome Succeeded { get; } = new(true, null);

    public static ActionOutcome Failed(string error) => new(false, error);
}

/// <summary>投稿内容。新規・返信・引用を 1 つの型で表す。</summary>
public sealed record ComposeRequest(
    string Text,
    IReadOnlyList<string> MediaPaths,
    string? ReplyToTweetId = null,
    string? QuoteTweetId = null);

/// <summary>添付 1 件の進捗。</summary>
public sealed record ComposeMediaProgress(string Path, string Phase, int Percent, string? Error);

/// <summary>投稿ジョブの進捗。</summary>
public sealed record ComposeProgress(string State, string Message, IReadOnlyList<ComposeMediaProgress> Items);

/// <summary>投稿結果。</summary>
public sealed record ComposeResult(bool Ok, string? TweetId, string Message);

/// <summary>書き込み系の API。UI は楽観的に先に反映し、失敗したら戻す。</summary>
public interface ITweetActionService
{
    Task<ActionOutcome> SetLikeAsync(string tweetId, bool like);

    Task<ActionOutcome> SetRetweetAsync(string tweetId, bool retweet);

    Task<ComposeResult> PostAsync(ComposeRequest request, IProgress<ComposeProgress>? progress = null, CancellationToken cancellationToken = default);
}
