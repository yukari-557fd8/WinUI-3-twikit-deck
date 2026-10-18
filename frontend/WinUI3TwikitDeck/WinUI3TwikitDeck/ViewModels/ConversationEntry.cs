using CommunityToolkit.Mvvm.ComponentModel;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.ViewModels;

/// <summary>会話表示の 1 行（ツイート / 取得できなかったツイート / 返信の見出し）（WinUI-3-twikit より移植）。</summary>
public sealed partial class ConversationEntry : ObservableObject
{
    /// <summary>上の行とつなぐ線（返信先が上の行）。</summary>
    [ObservableProperty]
    private bool _hasLineAbove;

    /// <summary>下の行とつなぐ線（下の行がこの行への返信）。</summary>
    [ObservableProperty]
    private bool _hasLineBelow;

    /// <summary>返信の見出しの下に「返信はありません」を出す（返信が 1 件も無いと確定したとき）。</summary>
    [ObservableProperty]
    private bool _showNoReplies;

    public TweetCardModel? Tweet { get; init; }

    /// <summary>開いているツイート。</summary>
    public bool IsFocal { get; init; }

    /// <summary>返信元のうち表示できないツイートの ID。この行はその代わりの表示。</summary>
    public string? MissingTweetId { get; init; }

    /// <summary>表示できない理由（X の説明文。X が返さなかっただけのときは既定の文言）。</summary>
    public string MissingText { get; init; } = "取得できなかったツイートがあります";

    /// <summary>
    /// X が流れの途中から返したなど、そのツイートの会話を開き直せば続きが出るかもしれない。
    /// 削除・凍結などで X が見られないと答えたものは開いても表示できないので false。
    /// </summary>
    public bool CanOpenMissing { get; init; }

    public bool IsRepliesHeader { get; init; }

    public bool IsTweet => Tweet is not null;

    public bool IsMissing => MissingTweetId is not null;
}
