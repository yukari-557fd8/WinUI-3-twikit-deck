using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

    /// <summary>保存されるデッキ全体の構成（カラムの並びと、カラム領域全体の設定）。</summary>
    public sealed class DeckLayout
    {
        /// <summary>カラム幅をウィンドウに合わせる（各カラムの幅設定を比率として配分する）。</summary>
        public bool FitColumnsToWindow { get; set; }

        public IReadOnlyList<DeckColumnDefinition> Columns { get; set; } = [];
    }

    /// <summary>カラム構成の保存・復元と、ファイルへのエクスポート / ファイルからのインポート。</summary>
    internal static class DeckLayoutStore
    {
        /// <summary>エクスポート時のファイル名の初期値。</summary>
        public const string ExportFileName = "WinUI3TwikitDeckState.json";

        /// <summary>インポートで受け付ける最大サイズ。カラム構成は数 KB なので、別のファイルを選んだときの保険。</summary>
        private const long MaxImportBytes = 1024 * 1024;

        /// <summary>ファイル形式のバージョン。構成ファイルの形を変えるときに上げる。</summary>
        private const int CurrentVersion = 1;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private sealed class LayoutFile
        {
            [JsonPropertyName("version")]
            public int Version { get; set; } = CurrentVersion;

            /// <summary>無い（古いファイル）ときは false = 固定幅。</summary>
            [JsonPropertyName("fit_columns_to_window")]
            public bool FitColumnsToWindow { get; set; }

            /// <summary>無い（カラム構成ではない JSON）ときは null。</summary>
            [JsonPropertyName("columns")]
            public List<DeckColumnDefinition?>? Columns { get; set; }
        }

        public static string Path => RepositoryPaths.DeckColumnsPath;

        /// <summary>保存済みの構成。ファイルが無い・壊れているときは null。</summary>
        public static DeckLayout? Load()
        {
            var path = Path;
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return Parse(File.ReadAllText(path));
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"DeckLayoutStore: {path} を読めません: {ex.Message}");
                return null;
            }
        }

        public static void Save(DeckLayout layout)
        {
            var path = Path;
            try
            {
                Write(path, layout);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"DeckLayoutStore: {path} に保存できません: {ex.Message}");
            }
        }

        /// <summary>
        /// 構成を任意のファイルへ書き出す。形式は <c>deck_columns.json</c> と同じ。
        /// 失敗したときは <see cref="IOException"/> / <see cref="UnauthorizedAccessException"/> を投げる。
        /// </summary>
        public static void Export(string path, DeckLayout layout) => Write(path, layout);

        /// <summary>
        /// <see cref="Export"/> が書き出したファイルを読む。
        /// 読めないとき（別の JSON、壊れている、新しすぎる、大きすぎる）は null を返し、理由を <paramref name="error"/> に入れる。
        /// 読めたときは、カラムの種類が不明なものも含めてそのまま返す（有効かどうかは呼び出し側が判断する）。
        /// </summary>
        public static DeckLayout? Import(string path, out string? error)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    error = "ファイルが見つかりません。";
                    return null;
                }

                if (info.Length > MaxImportBytes)
                {
                    error = "ファイルが大きすぎます。エクスポートしたカラム構成のファイルを選んでください。";
                    return null;
                }

                var text = File.ReadAllText(path);
                var file = ParseFile(text);
                if (file.Version > CurrentVersion)
                {
                    error = "新しいバージョンのアプリで書き出されたファイルのため、読み込めません。";
                    return null;
                }

                error = null;
                return ToLayout(file);
            }
            catch (JsonException)
            {
                error = "カラム構成のファイルではありません（JSON として読めないか、カラムの定義がありません）。";
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = $"ファイルを読めません: {ex.Message}";
                return null;
            }
        }

        private static void Write(string path, DeckLayout layout)
        {
            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var file = new LayoutFile
            {
                FitColumnsToWindow = layout.FitColumnsToWindow,
                Columns = [.. layout.Columns],
            };
            File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
        }

        private static DeckLayout Parse(string json) => ToLayout(ParseFile(json));

        /// <summary>JSON を読み、カラム構成のファイルでなければ <see cref="JsonException"/>。</summary>
        private static LayoutFile ParseFile(string json)
        {
            var file = JsonSerializer.Deserialize<LayoutFile>(json, JsonOptions);
            if (file?.Columns is null)
            {
                throw new JsonException("columns がありません。");
            }

            return file;
        }

        private static DeckLayout ToLayout(LayoutFile file) => new()
        {
            FitColumnsToWindow = file.FitColumnsToWindow,
            Columns = file.Columns?.Where(c => c is not null).Select(c => c!).ToList() ?? [],
        };
    }
}
