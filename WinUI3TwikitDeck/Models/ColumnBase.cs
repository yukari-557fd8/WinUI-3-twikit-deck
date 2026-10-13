namespace WinUI3TwikitDeck.Models;

public abstract record ColumnBase(string Header);

public sealed record HomeColumn() : ColumnBase("Home");

public sealed record SearchColumn(string Query) : ColumnBase($"Search: {Query}");
