using System.Text.Json.Nodes;

namespace WinUI3TwikitDeck.Models;

/// <summary>ログイン中ユーザーのリスト 1 件（カラム追加時の選択用）。</summary>
public sealed class TwitterListModel
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Mode { get; init; } = string.Empty;

    public int MemberCount { get; init; }

    public int SubscriberCount { get; init; }

    public bool IsPrivate => Mode.Equals("Private", System.StringComparison.OrdinalIgnoreCase);

    public string MemberInfo => $"{MemberCount} メンバー · {SubscriberCount} 購読者{(IsPrivate ? " · 非公開" : string.Empty)}";

    public static TwitterListModel FromJsonObject(JsonObject json)
        => new()
        {
            Id = json["id"]?.GetValue<string>() ?? string.Empty,
            Name = json["name"]?.GetValue<string>() ?? string.Empty,
            Description = json["description"]?.GetValue<string>() ?? string.Empty,
            Mode = json["mode"]?.GetValue<string>() ?? string.Empty,
            MemberCount = json["member_count"]?.GetValue<int>() ?? 0,
            SubscriberCount = json["subscriber_count"]?.GetValue<int>() ?? 0,
        };
}
