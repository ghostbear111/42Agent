/// <summary>
/// 地图视觉风格(正式游戏版) —— 从 StyleTest 试验场移植的全部可用风格
/// ----------------------------------------------------------------------------
/// 每种风格 = 「TileData + MapGenerator → 该格颜色」的纯函数 + 动画参数(Fx)。
/// ColorOf 带 MapGenerator 参数：🔴梯度类用它查邻居高度算梯度(山脊/坡度/坡向/晕渲等)；
///   🟢🟡类及基础类忽略该参数(只用格内 TileData 字段)。
/// 渲染时 ChunkManager 用选中 profile 的 ColorOf 给每格 SetColor，
/// 动画由 ScanlineFx shader 在 GPU 处理(零 CPU 每帧开销)。
///
/// 共 19 种(去掉不适合大地图的 ⛔ 雷达/扫描回波)：
///   0  基础(原始纯色块，移植前样式)
///   🟢 离散2：星图霓虹/红外卫星
///   🟡 连续7：全息青/热力/辐射/终端绿/赛博紫粉/赛博分层/数据点阵
///   🔴 梯度9：等高线/线框/山脊线/霓虹等高线/晕渲/坡度/田中明暗/坡向/赛博浮雕
/// 风格选择存 GameConfig.MapStyle.StyleId，设置面板可运行时切换。
/// </summary>
using System;
using System.Collections.Generic;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using UnityEngine;

namespace GalaxyAgent.Map
{
    /// <summary>风格动画参数(喂给 ScanlineFx shader)</summary>
    public class StyleFx
    {
        public float Mode = 0;
        public float ScanFreq = 0.12f;
        public float ScanSpeed = 0f;
        public float ScanAmp = 0f;
        public float ScanWidth = 0.85f;
        public Color ScanColor = new Color(0.3f, 0.95f, 1f, 1f);
        public float CenterX = 32f;
        public float CenterY = 32f;
        public float PulseAmp = 0f;
        public float PulseSpeed = 3f;
        public float FlickerAmp = 0f;
        public float FlickerSpeed = 12f;
    }

    /// <summary>单种地图视觉风格</summary>
    public class MapStyleProfile
    {
        public string Id;
        public string Name;
        /// <summary>颜色映射：(TileData, MapGenerator) → 该格颜色。MapGenerator 供梯度类查邻居高度</summary>
        public Func<TileData, MapGenerator, Color> ColorOf;
        public StyleFx Fx;
    }

    /// <summary>风格集合(19种)</summary>
    public static class MapStyleProfilePalette
    {
        public static readonly List<MapStyleProfile> All = BuildAll();
        public const string DefaultId = "starchart";

        public static MapStyleProfile GetById(string id)
        {
            if (!string.IsNullOrEmpty(id))
                for (int i = 0; i < All.Count; i++)
                    if (All[i].Id == id) return All[i];
            return All[1]; // 默认星图霓虹(index 1，0 是基础)
        }

        private static List<MapStyleProfile> BuildAll() => new List<MapStyleProfile>
        {
            Basic(),
            StarChart(), Infrared(),
            Hologram(), Thermal(), Radiation(), Matrix(), Cyberpunk(), CyberHypsometric(), DotMatrix(),
            Contour(), Wireframe(), RidgeLines(), NeonContour(), Hillshade(), SlopeHazard(),
            TanakaContour(), AspectFlow(), CyberRelief()
        };

        // ==================== 0. 基础(原始纯色块) ====================

        /// <summary>0. 基础：移植前的原始纯色块(每 TileType 一个固定色，无动画)</summary>
        private static MapStyleProfile Basic()
        {
            return new MapStyleProfile
            {
                Id = "basic",
                Name = "基础(纯色块)",
                ColorOf = (t, _) => TilePalette.GetTerrainColor(t.TileType),
                Fx = new StyleFx() // 全默认=无动画
            };
        }

        // ==================== 🟢 离散类 ====================

        private static MapStyleProfile StarChart() => new MapStyleProfile
        {
            Id = "starchart", Name = "星图霓虹",
            ColorOf = (t, _) => t.TileType switch
            {
                TileType.Mountain => new Color(0.1f, 1f, 0.95f),
                TileType.Lake => new Color(0.95f, 0.25f, 0.85f),
                TileType.Impassable => new Color(0.5f, 0.1f, 0.5f),
                TileType.Volcano => new Color(1f, 0.45f, 0.05f),
                TileType.Ruins => new Color(0.75f, 0.4f, 1f),
                TileType.CrystalDesert => new Color(0.55f, 0.9f, 1f),
                TileType.Canyon => new Color(0.95f, 0.8f, 0.2f),
                _ => new Color(0.09f, 0.11f, 0.16f)
            },
            Fx = new StyleFx { PulseAmp = 0.07f, PulseSpeed = 2f, FlickerAmp = 0.09f, FlickerSpeed = 6f }
        };

        private static MapStyleProfile Infrared() => new MapStyleProfile
        {
            Id = "infrared", Name = "红外卫星",
            ColorOf = (t, _) => t.Biome switch
            {
                BiomeType.Forest => new Color(0.95f, 0.12f, 0.1f),
                BiomeType.Grassland => new Color(0.8f, 0.35f, 0.18f),
                BiomeType.Swamp => new Color(0.7f, 0.25f, 0.3f),
                BiomeType.Desert => new Color(0.9f, 0.72f, 0.3f),
                BiomeType.Tundra => new Color(0.92f, 0.95f, 1f),
                BiomeType.Volcanic => new Color(1f, 0.4f, 0f),
                BiomeType.CrystalWaste => new Color(0.55f, 0.4f, 0.6f),
                BiomeType.RuinField => new Color(0.45f, 0.4f, 0.55f),
                _ => new Color(0.3f, 0.4f, 0.6f)
            },
            Fx = new StyleFx { PulseAmp = 0.04f, PulseSpeed = 1.5f }
        };

        // ==================== 🟡 连续类(格内数值) ====================

        private static MapStyleProfile Hologram() => new MapStyleProfile
        {
            Id = "hologram", Name = "全息青",
            ColorOf = (t, _) =>
            {
                float v = Mathf.Clamp01(t.Height);
                float b = 0.25f + 0.75f * v;
                if ((t.Y & 1) == 0) b *= 0.65f;
                return new Color(0f, 0.85f * b, 1f * b, 1f);
            },
            Fx = new StyleFx
            {
                Mode = 0, ScanFreq = 0.15f, ScanSpeed = 0.5f, ScanAmp = 0.45f, ScanWidth = 0.8f,
                ScanColor = new Color(0f, 0.95f, 1f, 1f), PulseAmp = 0.04f, PulseSpeed = 4f
            }
        };

        private static MapStyleProfile Thermal() => new MapStyleProfile
        {
            Id = "thermal", Name = "热力伪色",
            ColorOf = (t, _) => Jet(Mathf.Clamp01((t.Temperature + 20f) / 110f)),
            Fx = new StyleFx { PulseAmp = 0.06f, PulseSpeed = 2.5f }
        };

        private static MapStyleProfile Radiation() => new MapStyleProfile
        {
            Id = "radiation", Name = "辐射热图",
            ColorOf = (t, _) => Gradient(Mathf.Clamp01(t.Radiation),
                new Color(0.02f, 0.02f, 0.04f), new Color(0.18f, 0f, 0.35f),
                new Color(0.85f, 0.1f, 0.55f), new Color(1f, 0.5f, 0.1f),
                new Color(1f, 0.95f, 0.3f), new Color(1f, 1f, 1f)),
            Fx = new StyleFx { PulseAmp = 0.08f, PulseSpeed = 3.5f, FlickerAmp = 0.05f, FlickerSpeed = 8f }
        };

        private static MapStyleProfile Matrix() => new MapStyleProfile
        {
            Id = "matrix", Name = "终端绿",
            ColorOf = (t, _) =>
            {
                float v = Mathf.Clamp01(t.Height);
                float b = 0.15f + 0.85f * v;
                if ((t.Y & 1) == 0) b *= 0.7f;
                return new Color(0f, b, 0.18f * b, 1f);
            },
            Fx = new StyleFx
            {
                Mode = 0, ScanFreq = 0.12f, ScanSpeed = 0.85f, ScanAmp = 0.35f, ScanWidth = 0.78f,
                ScanColor = new Color(0.1f, 1f, 0.4f, 1f), PulseAmp = 0.05f, PulseSpeed = 5f,
                FlickerAmp = 0.04f, FlickerSpeed = 10f
            }
        };

        private static MapStyleProfile Cyberpunk() => new MapStyleProfile
        {
            Id = "cyberpunk", Name = "赛博紫粉",
            ColorOf = (t, _) =>
            {
                float v = Mathf.Clamp01(t.Height);
                float hue = v < 0.5f ? Mathf.Lerp(0.80f, 0.92f, v * 2f) : Mathf.Lerp(0.92f, 0.50f, (v - 0.5f) * 2f);
                return Color.HSVToRGB(hue, 0.85f, 0.35f + 0.65f * v);
            },
            Fx = new StyleFx
            {
                Mode = 0, ScanFreq = 0.1f, ScanSpeed = 0.25f, ScanAmp = 0.22f, ScanWidth = 0.82f,
                ScanColor = new Color(0.9f, 0.2f, 0.8f, 1f), PulseAmp = 0.05f, PulseSpeed = 2.5f
            }
        };

        private static MapStyleProfile CyberHypsometric() => new MapStyleProfile
        {
            Id = "cyberhypsometric", Name = "赛博分层",
            ColorOf = (t, _) => Gradient(Mathf.Clamp01(t.Height),
                new Color(0.03f, 0.01f, 0.12f), new Color(0.18f, 0f, 0.4f),
                new Color(0.55f, 0.1f, 0.55f), new Color(0.1f, 0.65f, 0.9f),
                new Color(0.75f, 0.95f, 1f)),
            Fx = new StyleFx { PulseAmp = 0.04f, PulseSpeed = 2f }
        };

        private static MapStyleProfile DotMatrix() => new MapStyleProfile
        {
            Id = "dotmatrix", Name = "数据点阵",
            ColorOf = (t, _) =>
            {
                if ((t.X % 2 == 0) && (t.Y % 2 == 0))
                {
                    float v = Mathf.Clamp01(t.Height);
                    return new Color(0f, 0.85f, 1f) * (0.3f + 0.7f * v);
                }
                return new Color(0.02f, 0.03f, 0.05f);
            },
            Fx = new StyleFx { FlickerAmp = 0.1f, FlickerSpeed = 15f, PulseAmp = 0.02f }
        };

        // ==================== 🔴 梯度类(查邻居高度) ====================

        /// <summary>等高线：高度分带 + 等值线(格内 height，不需邻居)</summary>
        private static MapStyleProfile Contour() => new MapStyleProfile
        {
            Id = "contour", Name = "等高线",
            ColorOf = (t, _) =>
            {
                float v = Mathf.Clamp01(t.Height);
                Color base_ = Gradient(v,
                    new Color(0.05f, 0.12f, 0.22f), new Color(0.15f, 0.35f, 0.2f),
                    new Color(0.5f, 0.55f, 0.2f), new Color(0.6f, 0.45f, 0.25f),
                    new Color(0.85f, 0.85f, 0.85f));
                float band = v * 8f;
                if (Mathf.Abs(band - Mathf.Round(band)) < 0.06f) base_ = Color.Lerp(base_, Color.black, 0.55f);
                return base_;
            },
            Fx = new StyleFx { PulseAmp = 0.02f, PulseSpeed = 1.5f }
        };

        /// <summary>线框：高度梯度亮线 + 网格点</summary>
        private static MapStyleProfile Wireframe() => new MapStyleProfile
        {
            Id = "wireframe", Name = "线框",
            ColorOf = (t, mg) =>
            {
                HeightGrad(mg, t.X, t.Y, out float dx, out float dy);
                float grad = Mathf.Abs(dx) + Mathf.Abs(dy);
                Color line = new Color(0f, 0.9f, 1f);
                Color c = Color.Lerp(new Color(0.04f, 0.06f, 0.09f), line, Mathf.Clamp01(grad * 14f));
                if ((t.X % 8 == 0) && (t.Y % 8 == 0)) c = Color.Lerp(c, line, 0.8f);
                return c;
            },
            Fx = new StyleFx { PulseAmp = 0.05f, PulseSpeed = 3f, FlickerAmp = 0.07f, FlickerSpeed = 10f }
        };

        /// <summary>山脊线：梯度脊线亮青 + 山谷暗</summary>
        private static MapStyleProfile RidgeLines() => new MapStyleProfile
        {
            Id = "ridgelines", Name = "山脊线",
            ColorOf = (t, mg) =>
            {
                HeightGrad(mg, t.X, t.Y, out float dx, out float dy);
                float grad = Mathf.Sqrt(dx * dx + dy * dy);
                float ridge = Mathf.SmoothStep(0.015f, 0.05f, grad);
                Color c = Color.Lerp(new Color(0.04f, 0.03f, 0.08f), new Color(0f, 0.95f, 1f), ridge);
                c *= 0.6f + 0.6f * Mathf.Clamp01(t.Height);
                return c;
            },
            Fx = new StyleFx
            {
                Mode = 0, ScanFreq = 0.1f, ScanSpeed = 0.3f, ScanAmp = 0.2f, ScanWidth = 0.85f,
                ScanColor = new Color(0f, 0.9f, 1f, 1f), PulseAmp = 0.04f, PulseSpeed = 2.5f
            }
        };

        /// <summary>霓虹等高线：深底 + 霓虹等高线带(青/品红交替，格内 height)</summary>
        private static MapStyleProfile NeonContour() => new MapStyleProfile
        {
            Id = "neoncontour", Name = "霓虹等高线",
            ColorOf = (t, _) =>
            {
                float v = Mathf.Clamp01(t.Height);
                float band = v * 10f;
                int bi = Mathf.RoundToInt(band);
                if (Mathf.Abs(band - bi) < 0.04f)
                    return (bi % 2 == 0) ? new Color(0f, 0.9f, 1f) : new Color(0.95f, 0.2f, 0.85f);
                return Color.Lerp(new Color(0.03f, 0.02f, 0.06f), new Color(0.1f, 0.05f, 0.15f), v);
            },
            Fx = new StyleFx { PulseAmp = 0.05f, PulseSpeed = 2f, FlickerAmp = 0.06f, FlickerSpeed = 7f }
        };

        /// <summary>晕渲立体：西北光源法线明暗浮雕</summary>
        private static MapStyleProfile Hillshade() => new MapStyleProfile
        {
            Id = "hillshade", Name = "晕渲立体",
            ColorOf = (t, mg) =>
            {
                HeightGrad(mg, t.X, t.Y, out float dx, out float dy);
                float shade = Mathf.Clamp01(0.5f - dx * 3f - dy * 3f);
                return new Color(0f, 0.5f, 0.7f) * (0.2f + 0.95f * shade);
            },
            Fx = new StyleFx { PulseAmp = 0.03f, PulseSpeed = 1.5f }
        };

        /// <summary>坡度险区：陡坡霓虹品红高亮 + 缓坡暗紫</summary>
        private static MapStyleProfile SlopeHazard() => new MapStyleProfile
        {
            Id = "slopehazard", Name = "坡度险区",
            ColorOf = (t, mg) =>
            {
                HeightGrad(mg, t.X, t.Y, out float dx, out float dy);
                float grad = Mathf.Sqrt(dx * dx + dy * dy);
                float danger = Mathf.SmoothStep(0.02f, 0.08f, grad);
                return Color.Lerp(new Color(0.08f, 0.03f, 0.12f), new Color(1f, 0.2f, 0.5f), danger);
            },
            Fx = new StyleFx { PulseAmp = 0.06f, PulseSpeed = 3.5f, FlickerAmp = 0.05f, FlickerSpeed = 9f }
        };

        /// <summary>田中明暗：等高线受光面亮(青)/背光面暗(紫)</summary>
        private static MapStyleProfile TanakaContour() => new MapStyleProfile
        {
            Id = "tanaka", Name = "田中明暗",
            ColorOf = (t, mg) =>
            {
                float v = Mathf.Clamp01(t.Height);
                float band = v * 10f;
                int bi = Mathf.RoundToInt(band);
                Color c = Color.Lerp(new Color(0.04f, 0.03f, 0.08f), new Color(0.08f, 0.06f, 0.14f), v);
                if (Mathf.Abs(band - bi) < 0.05f)
                {
                    HeightGrad(mg, t.X, t.Y, out float dx, out float dy);
                    c = (dx + dy < 0) ? new Color(0f, 0.95f, 1f) : new Color(0.3f, 0.05f, 0.4f);
                }
                return c;
            },
            Fx = new StyleFx { PulseAmp = 0.03f, PulseSpeed = 2f }
        };

        /// <summary>坡向流场：梯度方向→色相(北青南品红)</summary>
        private static MapStyleProfile AspectFlow() => new MapStyleProfile
        {
            Id = "aspectflow", Name = "坡向流场",
            ColorOf = (t, mg) =>
            {
                HeightGrad(mg, t.X, t.Y, out float dx, out float dy);
                float ang = Mathf.Atan2(dy, dx);
                float hue = Mathf.Repeat(ang / 6.2831853f + 0.5f, 1f);
                return Color.HSVToRGB(hue, 0.7f, 0.3f + 0.6f * Mathf.Clamp01(t.Height));
            },
            Fx = new StyleFx { PulseAmp = 0.04f, PulseSpeed = 2f }
        };

        /// <summary>赛博浮雕：柔和梯度着色 + 紫调立体</summary>
        private static MapStyleProfile CyberRelief() => new MapStyleProfile
        {
            Id = "cyberrelief", Name = "赛博浮雕",
            ColorOf = (t, mg) =>
            {
                HeightGrad(mg, t.X, t.Y, out float dx, out float dy);
                float shade = Mathf.Clamp01(0.5f + dx * 1.5f + dy * 1.5f);
                return new Color(0.4f, 0.15f, 0.6f) * (0.3f + 0.9f * shade);
            },
            Fx = new StyleFx { PulseAmp = 0.03f, PulseSpeed = 1.5f }
        };

        // ==================== 辅助 ====================

        /// <summary>中心差分高度梯度(查 MapGenerator 邻居，边界返回 null→0)</summary>
        private static void HeightGrad(MapGenerator mapGen, int x, int y, out float dx, out float dy)
        {
            float hl = mapGen.GetTileAt(x - 1, y)?.Height ?? 0f;
            float hr = mapGen.GetTileAt(x + 1, y)?.Height ?? 0f;
            float hd = mapGen.GetTileAt(x, y - 1)?.Height ?? 0f;
            float hu = mapGen.GetTileAt(x, y + 1)?.Height ?? 0f;
            dx = hr - hl;
            dy = hu - hd;
        }

        private static Color Jet(float t)
        {
            t = Mathf.Clamp01(t);
            float r = Mathf.Clamp01(1.5f - Mathf.Abs(4f * t - 3f));
            float g = Mathf.Clamp01(1.5f - Mathf.Abs(4f * t - 2f));
            float b = Mathf.Clamp01(1.5f - Mathf.Abs(4f * t - 1f));
            return new Color(r, g, b, 1f);
        }

        private static Color Gradient(float t, params Color[] keys)
        {
            if (keys == null || keys.Length == 0) return Color.magenta;
            if (keys.Length == 1) return keys[0];
            t = Mathf.Clamp01(t);
            float seg = t * (keys.Length - 1);
            int i = Mathf.FloorToInt(seg);
            if (i >= keys.Length - 1) return keys[keys.Length - 1];
            return Color.Lerp(keys[i], keys[i + 1], seg - i);
        }
    }
}
