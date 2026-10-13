namespace WinUI3TwikitDeck.Models;

public abstract record ColumnBase(string Header);

public sealed record HomeColumn() : ColumnBase("ホーム");

public sealed record SearchColumn(string Query) : ColumnBase($"検索: {Query}");

/// <param name="Type">all | mentions</param>
public sealed record NotificationsColumn(string Type)
    : ColumnBase(Type == NotificationType.Mentions ? "メンション" : "通知");

public sealed record ListColumn(string ListId, string Name) : ColumnBase($"リスト: {Name}");

public sealed record UserColumn(string ScreenName) : ColumnBase($"@{ScreenName.TrimStart('@')}");

public static class NotificationType
{
    public const string All = "all";
    public const string Mentions = "mentions";

    public static string Normalize(string? type)
        => string.Equals(type, Mentions, System.StringComparison.OrdinalIgnoreCase) ? Mentions : All;
}
