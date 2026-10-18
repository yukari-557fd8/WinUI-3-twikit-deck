using System.Collections.Generic;
using WinUI3TwikitDeck.Models;

namespace WinUI3TwikitDeck.Services;

/// <summary>
/// 会話の返信元の流れ・開いたツイートの 1 件。
/// <see cref="Tweet"/> が null なら削除・凍結などで見られないツイートで、<see cref="UnavailableText"/> が X の説明文。
/// </summary>
/// <param name="MissingParentId">返信先のうち X が返さなかったツイートの ID（返信先が直前の項目なら null）。</param>
public sealed record ConversationLink(string Id, TweetCardModel? Tweet, string? MissingParentId, string? UnavailableText);

/// <summary>
/// 会話 1 ページ分。1 ページ目は返信元の流れ（古い順）と開いたツイートを含み、続きのページは <see cref="Replies"/> だけ。
/// <see cref="Replies"/> の各要素は返信のまとまりで、上から順に返信の連鎖（先頭が開いたツイートへの返信）。
/// </summary>
public sealed record ConversationPage(
    IReadOnlyList<ConversationLink> Ancestors,
    ConversationLink? Focal,
    IReadOnlyList<IReadOnlyList<TweetCardModel>> Replies,
    string? NextCursor);
