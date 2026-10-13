using System;
using System.Collections.Generic;

namespace WinUI3TwikitDeck.Services;

/// <summary>
/// タイムライン 1 ページ分。<see cref="NextCursor"/> を次の呼び出しに渡すと続きが取れる。
/// 要素は <see cref="Models.TweetCardModel"/> または <see cref="Models.NotificationCardModel"/>。
/// </summary>
public sealed record TimelinePage(IReadOnlyList<object> Items, string? NextCursor)
{
    public static TimelinePage Empty { get; } = new(Array.Empty<object>(), null);

    public bool HasMore => !string.IsNullOrEmpty(NextCursor);
}
