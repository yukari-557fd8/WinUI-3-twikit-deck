using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinUI3TwikitDeck.Bridge
{
    /// <summary>保存されるカラム 1 本の定義（<c>data/deck_columns.json</c>）。</summary>
    public sealed class DeckColumnDefinition
    {
        /// <summary>home | search | notifications | list | user</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "home";

        [JsonPropertyName("timeline_type")]
        public string? TimelineType { get; set; }

        [JsonPropertyName("query")]
        public string? Query { get; set; }

        [JsonPropertyName("notification_type")]
        public string? NotificationType { get; set; }

        [JsonPropertyName("list_id")]
        public string? ListId { get; set; }

        [JsonPropertyName("list_name")]
        public string? ListName { get; set; }

        [JsonPropertyName("screen_name")]
        public string? ScreenName { get; set; }

        /// <summary>ユーザーカラムのタブ: tweets | replies | retweets | media</summary>
        [JsonPropertyName("user_tab")]
        public string? UserTab { get; set; }

        /// <summary>ユーザーカラムのメディアタブの絞り込み: photos | videos</summary>
        [JsonPropertyName("media_kind")]
        public string? MediaKind { get; set; }

        /// <summary>自動更新の間隔（秒）。0 は手動。</summary>
        [JsonPropertyName("refresh_interval_seconds")]
        public int RefreshIntervalSeconds { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; } = 380;
    }

    /// <summary>カラム構成の保存・復元。</summary>
    internal static class DeckLayoutStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private sealed class LayoutFile
        {
            [JsonPropertyName("version")]
            public int Version { get; set; } = 1;

            [JsonPropertyName("columns")]
            public List<DeckColumnDefinition> Columns { get; set; } = [];
        }

        public static string Path => RepositoryPaths.DeckColumnsPath;

        /// <summary>保存済みの構成。ファイルが無い・壊れているときは null。</summary>
        public static IReadOnlyList<DeckColumnDefinition>? Load()
        {
            var path = Path;
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var file = JsonSerializer.Deserialize<LayoutFile>(File.ReadAllText(path), JsonOptions);
                return file?.Columns;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"DeckLayoutStore: {path} を読めません: {ex.Message}");
                return null;
            }
        }

        public static void Save(IReadOnlyList<DeckColumnDefinition> columns)
        {
            var path = Path;
            try
            {
                var directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var file = new LayoutFile { Columns = new List<DeckColumnDefinition>(columns) };
                File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"DeckLayoutStore: {path} に保存できません: {ex.Message}");
            }
        }
    }
}
