using System;
using System.Globalization;

namespace WinUI3TwikitDeck.Helpers;

/// <summary>作成日時の表示（WinUI-3-twikit より移植）。Bridge は "yyyy/MM/dd HH:mm:ss"（JST）を返す。</summary>
public static class TimeDisplayHelper
{
    private static readonly string[] KnownFormats =
    [
        "yyyy/MM/dd HH:mm:ss",
        "yyyy/MM/dd HH:mm",
    ];

    public static ulong? TryParseSnowflakeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return ulong.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    public static DateTime? TryParse(string? createdAt)
    {
        if (string.IsNullOrWhiteSpace(createdAt))
        {
            return null;
        }

        if (DateTime.TryParseExact(createdAt, KnownFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
        {
            return dt;
        }

        if (DateTime.TryParse(createdAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt))
        {
            return dt;
        }

        return null;
    }

    public static string FormatAbsolute(DateTime dateTime)
        => dateTime.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);

    public static string FormatRelative(DateTime dateTime, DateTime? now = null)
    {
        var reference = now ?? DateTime.Now;
        var elapsed = reference - dateTime;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "たった今";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes}分前";
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            return $"{(int)elapsed.TotalHours}時間前";
        }

        return $"{(int)elapsed.TotalDays}日前";
    }

    public static string FormatAbsoluteDisplay(string? createdAt)
    {
        var parsed = TryParse(createdAt);
        return parsed is null ? createdAt ?? string.Empty : FormatAbsolute(parsed.Value);
    }

    public static string FormatRelativeDisplay(string? createdAt, DateTime? now = null)
    {
        var parsed = TryParse(createdAt);
        return parsed is null ? string.Empty : FormatRelative(parsed.Value, now);
    }

    public static string FormatNowForStorage()
        => DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
}
