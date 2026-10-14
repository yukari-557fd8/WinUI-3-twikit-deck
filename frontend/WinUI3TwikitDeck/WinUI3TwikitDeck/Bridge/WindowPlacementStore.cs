using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinUI3TwikitDeck.Bridge
{
    /// <summary>
    /// 保存されるウィンドウの配置（<c>data/deck_window.json</c>）。
    /// 位置は物理ピクセル（どのモニターに出すかを決めるため）、サイズは DIP
    /// （拡大率の違うモニターでも同じ見かけの大きさで復元するため）。
    /// </summary>
    public sealed class WindowPlacement
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        /// <summary>ウィンドウ左上の位置（物理ピクセル、仮想スクリーン座標）。</summary>
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        /// <summary>ウィンドウ外枠のサイズ（DIP）。</summary>
        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }

        /// <summary>保存時の表示倍率。復元時に保存位置のモニターを探す矩形の推定に使う。</summary>
        [JsonPropertyName("scale")]
        public double Scale { get; set; } = 1;

        [JsonPropertyName("is_maximized")]
        public bool IsMaximized { get; set; }

        public WindowPlacement Clone() => (WindowPlacement)MemberwiseClone();
    }

    /// <summary>ウィンドウ配置の保存・復元。</summary>
    internal static class WindowPlacementStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        public static string Path => RepositoryPaths.DeckWindowPath;

        /// <summary>保存済みの配置。ファイルが無い・壊れているときは null。</summary>
        public static WindowPlacement? Load()
        {
            var path = Path;
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(path), JsonOptions);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"WindowPlacementStore: {path} を読めません: {ex.Message}");
                return null;
            }
        }

        public static void Save(WindowPlacement placement)
        {
            var path = Path;
            try
            {
                var directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, JsonSerializer.Serialize(placement, JsonOptions));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"WindowPlacementStore: {path} に保存できません: {ex.Message}");
            }
        }
    }
}
