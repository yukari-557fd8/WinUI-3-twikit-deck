using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.Services;

/// <summary>
/// いいね・リツイートの楽観的更新。UI を先に反転し、送信に失敗したら元に戻して通知する。
/// UI スレッドから呼ぶこと（await 後も UI スレッドに戻る）。
/// </summary>
public static class TweetActions
{
    private static readonly HashSet<string> PendingLikes = new(StringComparer.Ordinal);
    private static readonly HashSet<string> PendingRetweets = new(StringComparer.Ordinal);
    private static readonly object Sync = new();

    private static bool TryBegin(HashSet<string> pending, string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        lock (Sync)
        {
            return pending.Add(id);
        }
    }

    private static void End(HashSet<string> pending, string id)
    {
        lock (Sync)
        {
            pending.Remove(id);
        }
    }

    public static async Task ToggleLikeAsync(TweetCardModel? tweet)
    {
        if (tweet is null || !AppServices.IsConfigured || !TryBegin(PendingLikes, tweet.Id))
        {
            return;
        }

        var wantLike = !tweet.IsLiked;
        tweet.ToggleLike();
        try
        {
            var outcome = await AppServices.Actions.SetLikeAsync(tweet.Id, wantLike);
            if (!outcome.Ok)
            {
                tweet.ToggleLike();
                AppNotifications.Error($"{(wantLike ? "いいね" : "いいね解除")}に失敗しました: {outcome.Error}");
            }
        }
        catch (Exception ex)
        {
            tweet.ToggleLike();
            Debug.WriteLine($"Like failed: {ex}");
            AppNotifications.Error($"いいねに失敗しました: {ex.Message}");
        }
        finally
        {
            End(PendingLikes, tweet.Id);
        }
    }

    public static async Task ToggleRetweetAsync(TweetCardModel? tweet)
    {
        if (tweet is null || tweet.IsUserProtected || !AppServices.IsConfigured || !TryBegin(PendingRetweets, tweet.Id))
        {
            return;
        }

        var wantRetweet = !tweet.IsRetweeted;
        tweet.ToggleRetweet();
        try
        {
            var outcome = await AppServices.Actions.SetRetweetAsync(tweet.Id, wantRetweet);
            if (!outcome.Ok)
            {
                tweet.ToggleRetweet();
                AppNotifications.Error($"{(wantRetweet ? "リツイート" : "リツイート取り消し")}に失敗しました: {outcome.Error}");
            }
        }
        catch (Exception ex)
        {
            tweet.ToggleRetweet();
            Debug.WriteLine($"Retweet failed: {ex}");
            AppNotifications.Error($"リツイートに失敗しました: {ex.Message}");
        }
        finally
        {
            End(PendingRetweets, tweet.Id);
        }
    }
}
