using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media.Imaging;

namespace WinUI3TwikitDeck.Helpers;

/// <summary>
/// BitmapImage の LRU キャッシュ（WinUI-3-twikit より移植）。
/// BitmapImage は UI スレッドでしか作れないため、呼び出しはバインディング評価時（UI スレッド）に限る。
/// </summary>
public static class ImageCache
{
    private const int MaxEntries = 480;

    private static readonly Dictionary<string, BitmapImage> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> Order = new();
    private static readonly Dictionary<string, LinkedListNode<string>> Nodes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public static BitmapImage? GetAvatar(string? imageUrl)
        => GetOrCreate(TwitterImageUrls.ForAvatar(imageUrl), 96);

    public static BitmapImage? GetOrCreate(string? imageUrl, int decodePixelWidth = 0)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var key = decodePixelWidth > 0 ? $"{imageUrl}|w{decodePixelWidth}" : imageUrl;
        lock (Gate)
        {
            if (Cache.TryGetValue(key, out var existing))
            {
                Touch(key);
                return existing;
            }
        }

        var bitmap = new BitmapImage();
        if (decodePixelWidth > 0)
        {
            bitmap.DecodePixelWidth = decodePixelWidth;
        }

        bitmap.UriSource = uri;

        lock (Gate)
        {
            if (Cache.TryGetValue(key, out var existing))
            {
                Touch(key);
                return existing;
            }

            Cache[key] = bitmap;
            Nodes[key] = Order.AddFirst(key);
            Trim();
            return bitmap;
        }
    }

    private static void Touch(string key)
    {
        if (!Nodes.TryGetValue(key, out var node))
        {
            return;
        }

        Order.Remove(node);
        Order.AddFirst(node);
    }

    private static void Trim()
    {
        while (Cache.Count > MaxEntries && Order.Last is { } oldest)
        {
            Order.RemoveLast();
            Nodes.Remove(oldest.Value);
            Cache.Remove(oldest.Value);
        }
    }
}
