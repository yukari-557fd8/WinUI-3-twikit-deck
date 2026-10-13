using System;
using System.Threading.Tasks;
using WinUI3TwikitDeck.Models;
using WinUI3TwikitDeck.ViewModels;

namespace WinUI3TwikitDeck.Services;

/// <summary>投稿画面を開くときの文脈。</summary>
public enum ComposeMode
{
    New,
    Reply,
    Quote,
}

public sealed record ComposeContext(ComposeMode Mode, TweetCardModel? Target = null, string InitialText = "");

/// <summary>
/// アプリ全体で共有するサービスの置き場。App.OnLaunched で設定する。
/// コントロール（TweetCard など）はバインディングの都合で DI できないため、ここから取る。
/// </summary>
public static class AppServices
{
    public static ITimelineService Timeline { get; private set; } = null!;

    public static ITweetActionService Actions { get; private set; } = null!;

    public static MainViewModel Deck { get; private set; } = null!;

    /// <summary>投稿ダイアログを開く。MainWindow が設定する（XamlRoot が必要なため）。</summary>
    public static Func<ComposeContext, Task>? OpenCompose { get; set; }

    public static bool IsConfigured { get; private set; }

    public static void Configure(ITimelineService timeline, ITweetActionService actions, MainViewModel deck)
    {
        Timeline = timeline;
        Actions = actions;
        Deck = deck;
        IsConfigured = true;
    }

    public static Task ComposeAsync(ComposeContext context)
        => OpenCompose?.Invoke(context) ?? Task.CompletedTask;
}
