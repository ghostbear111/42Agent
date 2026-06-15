/// <summary>
/// 地图视觉风格定义（StyleTest 风格试验场专用）
/// ----------------------------------------------------------------------------
/// 每种风格 = 「给定格子数据 → 该格颜色」的映射 + 该风格的动态动画参数(Fx)。
/// 全部纯代码生成，无需美术资源。共 20 种，分两批：
///   第一批(1-10)：科幻/伪色星球主题调性（全息/热力/辐射/星图/雷达等）
///   第二批(11-20)：地图绘制技法 + 科技/赛博朋克调性（山脊线/等高线/晕渲/坡度/坡向等）
/// 动画由 ScanlineFx shader 在 GPU 处理(零 CPU 开销)，本文件只负责配色 + 动画参数。
/// 所有映射基于地图真实数值(高度噪声/温度/辐射/生物群系)及高度梯度(山脊/坡度/坡向)。
/// </summary>
using System;
using System.Collections.Generic;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using UnityEngine;

namespace GalaxyAgent.StyleTest
{
    /// <summary>
    /// 风格动画参数(喂给 ScanlineFx shader)
    /// </summary>
    public class StyleFx
    {
        public float Mode = 0;            // 0=水平扫描带 1=径向旋转扫描
        public float ScanFreq = 0.12f;    // 扫描频率(空间密度)
        public float ScanSpeed = 0f;      // 扫描移动速度
        public float ScanAmp = 0f;        // 扫描带亮度增幅(0=关闭扫描)
        public float ScanWidth = 0.85f;   // 扫描带宽度阈值
        public Color ScanColor = new Color(0.3f, 0.95f, 1f, 1f); // 扫描叠加色
        public float CenterX = 32f;       // 径向中心(格子坐标，预览中心)
        public float CenterY = 32f;
        public float PulseAmp = 0f;       // 整体呼吸幅度(0=关闭)
        public float PulseSpeed = 3f;     // 呼吸速度
        public float FlickerAmp = 0f;     // 随机闪烁幅度(0=关闭)
        public float FlickerSpeed = 12f;  // 闪烁速度
    }

    /// <summary>
    /// 单种地图视觉风格
    /// </summary>
    public class MapStyle
    {
        public string Name;
        public string Desc;
        /// <summary>颜色映射：(瓦片数据, 高度噪声图, 格x, 格y) → 该格颜色</summary>
        public Func<TileData, float[,], int, int, Color> ColorOf;
        /// <summary>该风格的动画参数(可空)</summary>
        public StyleFx Fx;
    }

    /// <summary>
    /// 全部风格集合（静态只读，共 20 种）
    /// </summary>
    public static class MapStylePalette
    {
        public static readonly List<MapStyle> All = BuildAll();

        private static List<MapStyle> BuildAll()
        {
            var list = new List<MapStyle>
            {
                // ---- 第一批：科幻 / 伪色星球 ----
                Hologram(),
                Thermal(),
                Radiation(),
                Contour(),
                Matrix(),
                StarChart(),
                Cyberpunk(),
                Radar(),
                Infrared(),
                Wireframe(),
                // ---- 第二批：地图绘制技法（科技 / 赛博朋克）----
                RidgeLines(),
                NeonContour(),
                Hillshade(),
                CyberHypsometric(),
                SlopeHazard(),
                DotMatrix(),
                TanakaContour(),
                AspectFlow(),
                SweepEcho(),
                CyberRelief()
            };
            return list;
        }

        // ==================== 第一批：科幻 / 伪色星球 ====================

        private static MapStyle Hologram()
        {
            return new MapStyle
            {
                Name = "全息青 Hologram",
                Desc = "青色单色·高度明暗·扫描线",
                ColorOf = (tile, h, x, y) =>
                {
                    float v = Mathf.Clamp01(h[x, y]);
                    float b = 0.25f + 0.75f * v;
                    if ((y & 1) == 0) b *= 0.65f;
                    return new Color(0f, 0.85f * b, 1f * b, 1f);
                },
                Fx = new StyleFx
                {
                    Mode = 0, ScanFreq = 0.15f, ScanSpeed = 0.5f, ScanAmp = 0.45f, ScanWidth = 0.8f,
                    ScanColor = new Color(0f, 0.95f, 1f, 1f),
                    PulseAmp = 0.04f, PulseSpeed = 4f
                }
            };
        }

        private static MapStyle Thermal()
        {
            return new MapStyle
            {
                Name = "热力伪色 Thermal",
                Desc = "温度→Jet色阶 蓝→绿→黄→红",
                ColorOf = (tile, h, x, y) => Jet(Mathf.Clamp01((tile.Temperature + 20f) / 110f)),
                Fx = new StyleFx { PulseAmp = 0.06f, PulseSpeed = 2.5f }
            };
        }

        private static MapStyle Radiation()
        {
            return new MapStyle
            {
                Name = "辐射热图 Radiation",
                Desc = "辐射→黑紫红黄白 危险高亮",
                ColorOf = (tile, h, x, y) => Gradient(Mathf.Clamp01(tile.Radiation),
                    new Color(0.02f, 0.02f, 0.04f),
                    new Color(0.18f, 0f, 0.35f),
                    new Color(0.85f, 0.1f, 0.55f),
                    new Color(1f, 0.5f, 0.1f),
                    new Color(1f, 0.95f, 0.3f),
                    new Color(1f, 1f, 1f)),
                Fx = new StyleFx { PulseAmp = 0.08f, PulseSpeed = 3.5f, FlickerAmp = 0.05f, FlickerSpeed = 8f }
            };
        }

        private static MapStyle Contour()
        {
            return new MapStyle
            {
                Name = "等高线 Contour",
                Desc = "高度分带·等值线描边·地形图",
                ColorOf = (tile, h, x, y) =>
                {
                    float v = Mathf.Clamp01(h[x, y]);
                    Color base_ = Gradient(v,
                        new Color(0.05f, 0.12f, 0.22f),
                        new Color(0.15f, 0.35f, 0.2f),
                        new Color(0.5f, 0.55f, 0.2f),
                        new Color(0.6f, 0.45f, 0.25f),
                        new Color(0.85f, 0.85f, 0.85f));
                    float band = v * 8f;
                    if (Mathf.Abs(band - Mathf.Round(band)) < 0.06f) base_ = Color.Lerp(base_, Color.black, 0.55f);
                    return base_;
                },
                Fx = new StyleFx { PulseAmp = 0.02f, PulseSpeed = 1.5f }
            };
        }

        private static MapStyle Matrix()
        {
            return new MapStyle
            {
                Name = "终端绿 Matrix",
                Desc = "CRT绿单色·高度明暗·扫描线",
                ColorOf = (tile, h, x, y) =>
                {
                    float v = Mathf.Clamp01(h[x, y]);
                    float b = 0.15f + 0.85f * v;
                    if ((y & 1) == 0) b *= 0.7f;
                    return new Color(0f, b, 0.18f * b, 1f);
                },
                Fx = new StyleFx
                {
                    Mode = 0, ScanFreq = 0.12f, ScanSpeed = 0.85f, ScanAmp = 0.35f, ScanWidth = 0.78f,
                    ScanColor = new Color(0.1f, 1f, 0.4f, 1f),
                    PulseAmp = 0.05f, PulseSpeed = 5f, FlickerAmp = 0.04f, FlickerSpeed = 10f
                }
            };
        }

        private static MapStyle StarChart()
        {
            return new MapStyle
            {
                Name = "星图霓虹 StarChart",
                Desc = "深空底·山青·湖品红·火山橙",
                ColorOf = (tile, h, x, y) =>
                {
                    switch (tile.TileType)
                    {
                        case TileType.Mountain: return new Color(0.1f, 1f, 0.95f);
                        case TileType.Lake: return new Color(0.95f, 0.25f, 0.85f);
                        case TileType.Impassable: return new Color(0.5f, 0.1f, 0.5f);
                        case TileType.Volcano: return new Color(1f, 0.45f, 0.05f);
                        case TileType.Ruins: return new Color(0.75f, 0.4f, 1f);
                        case TileType.CrystalDesert: return new Color(0.55f, 0.9f, 1f);
                        case TileType.Canyon: return new Color(0.95f, 0.8f, 0.2f);
                        default: return new Color(0.09f, 0.11f, 0.16f);
                    }
                },
                Fx = new StyleFx { PulseAmp = 0.07f, PulseSpeed = 2f, FlickerAmp = 0.09f, FlickerSpeed = 6f }
            };
        }

        private static MapStyle Cyberpunk()
        {
            return new MapStyle
            {
                Name = "赛博紫粉 Cyberpunk",
                Desc = "深紫底·品红青霓虹·高度调色相",
                ColorOf = (tile, h, x, y) =>
                {
                    float v = Mathf.Clamp01(h[x, y]);
                    float hue = v < 0.5f
                        ? Mathf.Lerp(0.80f, 0.92f, v * 2f)
                        : Mathf.Lerp(0.92f, 0.50f, (v - 0.5f) * 2f);
                    return Color.HSVToRGB(hue, 0.85f, 0.35f + 0.65f * v);
                },
                Fx = new StyleFx
                {
                    Mode = 0, ScanFreq = 0.1f, ScanSpeed = 0.25f, ScanAmp = 0.22f, ScanWidth = 0.82f,
                    ScanColor = new Color(0.9f, 0.2f, 0.8f, 1f),
                    PulseAmp = 0.05f, PulseSpeed = 2.5f
                }
            };
        }

        private static MapStyle Radar()
        {
            const float cx = 31.5f, cy = 31.5f;
            return new MapStyle
            {
                Name = "雷达 Radar",
                Desc = "绿单色·径向衰减·同心环·旋转扫描",
                ColorOf = (tile, h, x, y) =>
                {
                    float dx = x - cx, dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / 45f;
                    float b = Mathf.Clamp01(1f - dist);
                    float ring = dist * 5f;
                    if (Mathf.Abs(ring - Mathf.Round(ring)) < 0.08f) b = Mathf.Max(b, 0.9f);
                    return new Color(0f, b * 0.95f, 0.25f * b, 1f);
                },
                Fx = new StyleFx
                {
                    Mode = 1, ScanSpeed = 0.7f, ScanAmp = 0.65f, ScanWidth = 0.86f,
                    ScanColor = new Color(0.4f, 1f, 0.4f, 1f),
                    CenterX = cx, CenterY = cy, PulseAmp = 0.03f, PulseSpeed = 2f
                }
            };
        }

        private static MapStyle Infrared()
        {
            return new MapStyle
            {
                Name = "红外卫星 Infrared",
                Desc = "生物群系遥感伪色·植被红·水黑",
                ColorOf = (tile, h, x, y) =>
                {
                    switch (tile.Biome)
                    {
                        case BiomeType.Forest: return new Color(0.95f, 0.12f, 0.1f);
                        case BiomeType.Grassland: return new Color(0.8f, 0.35f, 0.18f);
                        case BiomeType.Swamp: return new Color(0.7f, 0.25f, 0.3f);
                        case BiomeType.Desert: return new Color(0.9f, 0.72f, 0.3f);
                        case BiomeType.Tundra: return new Color(0.92f, 0.95f, 1f);
                        case BiomeType.Volcanic: return new Color(1f, 0.4f, 0f);
                        case BiomeType.CrystalWaste: return new Color(0.55f, 0.4f, 0.6f);
                        case BiomeType.RuinField: return new Color(0.45f, 0.4f, 0.55f);
                        default: return new Color(0.3f, 0.4f, 0.6f);
                    }
                },
                Fx = new StyleFx { PulseAmp = 0.04f, PulseSpeed = 1.5f }
            };
        }

        private static MapStyle Wireframe()
        {
            return new MapStyle
            {
                Name = "线框 Wireframe",
                Desc = "深底·高度梯度亮线·网格点",
                ColorOf = (tile, h, x, y) =>
                {
                    float here = h[x, y];
                    int M = h.GetLength(0);
                    float hx1 = (x + 1 < M) ? h[x + 1, y] : here;
                    float hy1 = (y + 1 < M) ? h[x, y + 1] : here;
                    float grad = Mathf.Abs(here - hx1) + Mathf.Abs(here - hy1);
                    Color line = new Color(0f, 0.9f, 1f);
                    Color dark = new Color(0.04f, 0.06f, 0.09f);
                    Color c = Color.Lerp(dark, line, Mathf.Clamp01(grad * 14f));
                    if ((x % 8 == 0) && (y % 8 == 0)) c = Color.Lerp(c, line, 0.8f);
                    return c;
                },
                Fx = new StyleFx { PulseAmp = 0.05f, PulseSpeed = 3f, FlickerAmp = 0.07f, FlickerSpeed = 10f }
            };
        }

        // ==================== 第二批：地图绘制技法（科技 / 赛博朋克）====================

        /// <summary>11. 山脊线：高度梯度脊线亮(青)，山谷暗，地形骨架</summary>
        private static MapStyle RidgeLines()
        {
            return new MapStyle
            {
                Name = "山脊线 Ridge",
                Desc = "梯度脊线亮青·山谷暗·地形骨架",
                ColorOf = (tile, h, x, y) =>
                {
                    HeightGrad(h, x, y, out float dx, out float dy);
                    float grad = Mathf.Sqrt(dx * dx + dy * dy);
                    float ridge = Mathf.SmoothStep(0.015f, 0.05f, grad);
                    Color deep = new Color(0.04f, 0.03f, 0.08f);
                    Color line = new Color(0f, 0.95f, 1f);
                    Color c = Color.Lerp(deep, line, ridge);
                    c *= 0.6f + 0.6f * Mathf.Clamp01(h[x, y]); // 高处更亮
                    return c;
                },
                Fx = new StyleFx
                {
                    Mode = 0, ScanFreq = 0.1f, ScanSpeed = 0.3f, ScanAmp = 0.2f, ScanWidth = 0.85f,
                    ScanColor = new Color(0f, 0.9f, 1f, 1f), PulseAmp = 0.04f, PulseSpeed = 2.5f
                }
            };
        }

        /// <summary>12. 霓虹等高线：深空底 + 霓虹等高线带(青/品红交替)，赛博地形图</summary>
        private static MapStyle NeonContour()
        {
            return new MapStyle
            {
                Name = "霓虹等高线 NeonContour",
                Desc = "深底·霓虹等高线带·青品红交替",
                ColorOf = (tile, h, x, y) =>
                {
                    float v = Mathf.Clamp01(h[x, y]);
                    float band = v * 10f;
                    int bi = Mathf.RoundToInt(band);
                    float frac = Mathf.Abs(band - bi);
                    Color deep = new Color(0.03f, 0.02f, 0.06f);
                    if (frac < 0.04f)
                        return (bi % 2 == 0) ? new Color(0f, 0.9f, 1f) : new Color(0.95f, 0.2f, 0.85f);
                    return Color.Lerp(deep, new Color(0.1f, 0.05f, 0.15f), v);
                },
                Fx = new StyleFx { PulseAmp = 0.05f, PulseSpeed = 2f, FlickerAmp = 0.06f, FlickerSpeed = 7f }
            };
        }

        /// <summary>13. 晕渲立体：模拟西北光源的法线明暗，立体浮雕(青调)</summary>
        private static MapStyle Hillshade()
        {
            return new MapStyle
            {
                Name = "晕渲立体 Hillshade",
                Desc = "西北光源·法线明暗·立体浮雕",
                ColorOf = (tile, h, x, y) =>
                {
                    HeightGrad(h, x, y, out float dx, out float dy);
                    float shade = Mathf.Clamp01(0.5f - dx * 3f - dy * 3f);
                    return new Color(0f, 0.5f, 0.7f) * (0.2f + 0.95f * shade);
                },
                Fx = new StyleFx { PulseAmp = 0.03f, PulseSpeed = 1.5f }
            };
        }

        /// <summary>14. 赛博分层设色：海拔分层赛博渐变(深紫低→品红→青→白高)</summary>
        private static MapStyle CyberHypsometric()
        {
            return new MapStyle
            {
                Name = "赛博分层 Hypsometric",
                Desc = "海拔分层·深紫→品红→青→白高",
                ColorOf = (tile, h, x, y) => Gradient(Mathf.Clamp01(h[x, y]),
                    new Color(0.03f, 0.01f, 0.12f),
                    new Color(0.18f, 0f, 0.4f),
                    new Color(0.55f, 0.1f, 0.55f),
                    new Color(0.1f, 0.65f, 0.9f),
                    new Color(0.75f, 0.95f, 1f)),
                Fx = new StyleFx { PulseAmp = 0.04f, PulseSpeed = 2f }
            };
        }

        /// <summary>15. 坡度险区：陡坡霓虹高亮(品红)，缓坡暗紫，危险坡度图</summary>
        private static MapStyle SlopeHazard()
        {
            return new MapStyle
            {
                Name = "坡度险区 Slope",
                Desc = "陡坡霓虹品红高亮·缓坡暗紫",
                ColorOf = (tile, h, x, y) =>
                {
                    HeightGrad(h, x, y, out float dx, out float dy);
                    float grad = Mathf.Sqrt(dx * dx + dy * dy);
                    float danger = Mathf.SmoothStep(0.02f, 0.08f, grad);
                    return Color.Lerp(new Color(0.08f, 0.03f, 0.12f), new Color(1f, 0.2f, 0.5f), danger);
                },
                Fx = new StyleFx { PulseAmp = 0.06f, PulseSpeed = 3.5f, FlickerAmp = 0.05f, FlickerSpeed = 9f }
            };
        }

        /// <summary>16. 数据点阵：按高度密度的点阵图，每隔2格亮点，数据刷新感</summary>
        private static MapStyle DotMatrix()
        {
            return new MapStyle
            {
                Name = "数据点阵 DotMatrix",
                Desc = "高度密度点阵·每隔2格亮点·数据感",
                ColorOf = (tile, h, x, y) =>
                {
                    if ((x % 2 == 0) && (y % 2 == 0))
                    {
                        float v = Mathf.Clamp01(h[x, y]);
                        return new Color(0f, 0.85f, 1f) * (0.3f + 0.7f * v);
                    }
                    return new Color(0.02f, 0.03f, 0.05f);
                },
                Fx = new StyleFx { FlickerAmp = 0.1f, FlickerSpeed = 15f, PulseAmp = 0.02f }
            };
        }

        /// <summary>17. 田中明暗等高线：等高线受光面亮(青)/背光面暗(紫)，立体等高线</summary>
        private static MapStyle TanakaContour()
        {
            return new MapStyle
            {
                Name = "田中明暗 Tanaka",
                Desc = "等高线受光面亮·背光面暗·立体",
                ColorOf = (tile, h, x, y) =>
                {
                    float v = Mathf.Clamp01(h[x, y]);
                    float band = v * 10f;
                    int bi = Mathf.RoundToInt(band);
                    float frac = Mathf.Abs(band - bi);
                    Color c = Color.Lerp(new Color(0.04f, 0.03f, 0.08f), new Color(0.08f, 0.06f, 0.14f), v);
                    if (frac < 0.05f)
                    {
                        HeightGrad(h, x, y, out float dx, out float dy);
                        c = (dx + dy < 0) ? new Color(0f, 0.95f, 1f) : new Color(0.3f, 0.05f, 0.4f);
                    }
                    return c;
                },
                Fx = new StyleFx { PulseAmp = 0.03f, PulseSpeed = 2f }
            };
        }

        /// <summary>18. 坡向流场：梯度方向→色相(北青南品红)，科技坡向图</summary>
        private static MapStyle AspectFlow()
        {
            return new MapStyle
            {
                Name = "坡向流场 Aspect",
                Desc = "梯度方向→色相·北青南品红·坡向",
                ColorOf = (tile, h, x, y) =>
                {
                    HeightGrad(h, x, y, out float dx, out float dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float hue = Mathf.Repeat(ang / 6.2831853f + 0.5f, 1f);
                    float v = Mathf.Clamp01(h[x, y]);
                    return Color.HSVToRGB(hue, 0.7f, 0.3f + 0.6f * v);
                },
                Fx = new StyleFx { PulseAmp = 0.04f, PulseSpeed = 2f }
            };
        }

        /// <summary>19. 扫描回波：暗底 + 强径向旋转扫描，高度回波亮度，赛博雷达数据</summary>
        private static MapStyle SweepEcho()
        {
            return new MapStyle
            {
                Name = "扫描回波 SweepEcho",
                Desc = "暗底·强径向旋转扫描·高度回波",
                ColorOf = (tile, h, x, y) =>
                {
                    float v = Mathf.Clamp01(h[x, y]);
                    return new Color(0f, 0.8f, 1f) * (v * 0.7f); // 基础暗，靠 shader 扫描突出
                },
                Fx = new StyleFx
                {
                    Mode = 1, ScanSpeed = 1.2f, ScanAmp = 0.9f, ScanWidth = 0.9f,
                    ScanColor = new Color(0f, 1f, 0.9f, 1f),
                    CenterX = 32f, CenterY = 32f, PulseAmp = 0.05f
                }
            };
        }

        /// <summary>20. 赛博浮雕：多方向柔和梯度着色，紫调立体地形</summary>
        private static MapStyle CyberRelief()
        {
            return new MapStyle
            {
                Name = "赛博浮雕 CyberRelief",
                Desc = "柔和梯度着色·紫调立体·浮雕感",
                ColorOf = (tile, h, x, y) =>
                {
                    HeightGrad(h, x, y, out float dx, out float dy);
                    float shade = Mathf.Clamp01(0.5f + dx * 1.5f + dy * 1.5f);
                    return new Color(0.4f, 0.15f, 0.6f) * (0.3f + 0.9f * shade);
                },
                Fx = new StyleFx { PulseAmp = 0.03f, PulseSpeed = 1.5f }
            };
        }

        // ==================== 梯度 / 色阶辅助 ====================

        /// <summary>中心差分高度梯度(边界用自身近似)</summary>
        private static void HeightGrad(float[,] h, int x, int y, out float dx, out float dy)
        {
            int M = h.GetLength(0);
            float hl = x > 0 ? h[x - 1, y] : h[x, y];
            float hr = x < M - 1 ? h[x + 1, y] : h[x, y];
            float hd = y > 0 ? h[x, y - 1] : h[x, y];
            float hu = y < M - 1 ? h[x, y + 1] : h[x, y];
            dx = hr - hl;
            dy = hu - hd;
        }

        /// <summary>Jet 色阶（蓝→青→绿→黄→红），t∈[0,1]</summary>
        private static Color Jet(float t)
        {
            t = Mathf.Clamp01(t);
            float r = Mathf.Clamp01(1.5f - Mathf.Abs(4f * t - 3f));
            float g = Mathf.Clamp01(1.5f - Mathf.Abs(4f * t - 2f));
            float b = Mathf.Clamp01(1.5f - Mathf.Abs(4f * t - 1f));
            return new Color(r, g, b, 1f);
        }

        /// <summary>多段渐变：t∈[0,1] 在 keys 间线性插值</summary>
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
