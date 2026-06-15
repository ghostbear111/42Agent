/// <summary>
/// 地图图片导出器：把整张地图渲染成一张 PNG(俯视，颜色编码地形)。
/// ----------------------------------------------------------------------------
/// 用途：导出后喂给 AI 图生图，生成美术地图；或作为地图预览/存档。
/// 实现：遍历所有格按地形/风格算颜色 → SetPixel 写 Texture2D → EncodeToPNG。
/// 地图最大 2048²，单张图完全放得下(GPU 纹理上限 16384²)。
///
/// 默认纯色块(每 TileType 一个色，分区清晰)，最适合给 AI 当参考；
/// 可传 MapStyleProfile 用风格配色(连续伪色/梯度)，但 per-cell 需查 MapGenerator。
/// </summary>
using System.IO;
using GalaxyAgent.Data.Models;
using UnityEngine;

namespace GalaxyAgent.Map
{
    public static class MapImageExporter
    {
        /// <summary>
        /// 导出整张地图为 PNG。
        /// </summary>
        /// <param name="mapGen">已生成的地图(所有 chunk 数据须已 Generate)</param>
        /// <param name="config">地图配置(取 MapWidth/PlanetName)</param>
        /// <param name="seed">种子(用于文件名)</param>
        /// <param name="profile">视觉风格(null=纯色块，每 TileType 一个色，最适合 AI 参考)</param>
        /// <returns>PNG 文件绝对路径；失败返回 null</returns>
        public static string Export(MapGenerator mapGen, MapConfig config, int seed, MapStyleProfile profile = null)
        {
            if (mapGen == null || config == null) return null;
            int w = config.MapWidth;

            var tex = new Texture2D(w, w, TextureFormat.RGB24, false);
            var pixels = new Color[w * w];

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < w; y++)
                {
                    var tile = mapGen.GetTileAt(x, y);
                    // 空白格(超边界)=深空底；有格=纯色块或风格配色
                    Color c = new Color(0.03f, 0.04f, 0.08f);
                    if (tile != null)
                        c = profile != null ? profile.ColorOf(tile, mapGen) : TilePalette.GetTerrainColor(tile.TileType);
                    // PNG 习惯左上为 y 最大，Unity Texture2D 原点左下 → 翻转 y
                    pixels[(w - 1 - y) * w + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            // 输出到 Assets/MapExports/ (编辑器可见，方便取图喂 AI)
            string dir = Path.Combine(Application.dataPath, "MapExports");
            Directory.CreateDirectory(dir);
            string name = string.IsNullOrEmpty(config.PlanetName) ? "map" : SanitizeFileName(config.PlanetName);
            string path = Path.Combine(dir, $"{name}_{seed}_{w}x{w}.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());

#if UNITY_EDITOR
            // 编辑器下刷新资产数据库，让 Project 窗口立即看到新图
            UnityEditor.AssetDatabase.Refresh();
#endif
            Object.DestroyImmediate(tex);
            return path;
        }

        /// <summary>把文件名非法字符替换为下划线</summary>
        private static string SanitizeFileName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }
    }
}
