/// <summary>
/// 运行时 Sprite 资源注册表（懒加载 + 缓存）
/// 集中按枚举加载 Assets/Resources/Sprites/ 下的占位图 Sprite，供 UI 复用。
///
/// 设计要点：
/// - 用 Resources.LoadAll&lt;Sprite&gt;()[0] 加载：兼容 Sprite 导入为 Single 或 Multiple 两种模式。
///   用户替换 PNG 时 Unity 可能重置 .meta 的 Sprite Mode，LoadAll 对两种模式都返回首个 Sprite，最健壮。
/// - 加载失败返回 null，由 ApplySpriteOrColor 降级为原色块，绝不崩溃。
/// - 强类型访问（GetAvatar/GetBaseAvatar/GetResource/GetWeather），杜绝拼写错。
///
/// 占位图清单（15 张，128×128，位于 Assets/Resources/Sprites/）：
///   头像: avatar_scout / avatar_worker / avatar_guard / avatar_base
///   资源: icon_mineral / icon_crystal / icon_water / icon_organic / icon_ruin
///   天气: weather_clear / weather_sandstorm / weather_acidrain / weather_coldwave / weather_magneticstorm / weather_blizzard
/// 替换正式美术：覆盖同名 PNG、保持文件名不变即可，无需改代码。
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Data.Enums;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public static class SpriteRegistry
    {
        /// <summary>Resources 子目录前缀（对应 Assets/Resources/Sprites/）</summary>
        private const string DIR = "Sprites/";

        /// <summary>Sprite 缓存（含 null 缓存，避免对缺失资源反复 Load 并反复告警）</summary>
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        // ==================== 强类型访问 ====================

        /// <summary>Agent 头像（Scout/Worker/Guard；其它类型返回 null）</summary>
        public static Sprite GetAvatar(AgentType t) => t switch
        {
            AgentType.Scout => Get("avatar_scout"),
            AgentType.Worker => Get("avatar_worker"),
            AgentType.Guard => Get("avatar_guard"),
            _ => null
        };

        /// <summary>基地头像</summary>
        public static Sprite GetBaseAvatar() => Get("avatar_base");

        /// <summary>资源图标（注意：RuinData → icon_ruin）</summary>
        public static Sprite GetResource(ResourceType t) => t switch
        {
            ResourceType.Mineral => Get("icon_mineral"),
            ResourceType.Crystal => Get("icon_crystal"),
            ResourceType.Water => Get("icon_water"),
            ResourceType.Organic => Get("icon_organic"),
            ResourceType.RuinData => Get("icon_ruin"),
            _ => null
        };

        /// <summary>天气图标（驼峰枚举名 ↔ 连写文件名：AcidRain→acidrain 等）</summary>
        public static Sprite GetWeather(WeatherType t) => t switch
        {
            WeatherType.Clear => Get("weather_clear"),
            WeatherType.Sandstorm => Get("weather_sandstorm"),
            WeatherType.AcidRain => Get("weather_acidrain"),
            WeatherType.ColdWave => Get("weather_coldwave"),
            WeatherType.MagneticStorm => Get("weather_magneticstorm"),
            WeatherType.Blizzard => Get("weather_blizzard"),
            _ => null
        };

        /// <summary>场景全屏背景（mainmenu / mapgen）</summary>
        public static Sprite GetSceneBg(string name) => Get("bg_" + name);

        /// <summary>通用 UI 面板底纹（9-slice，CreatePanel 默认用）</summary>
        public static Sprite GetPanelSkin() => Get("panel_bg");

        /// <summary>通用按钮皮肤（9-slice，CreateButton 默认用）</summary>
        public static Sprite GetButtonSkin() => Get("btn_bg");

        /// <summary>功能按钮图标（pause/config/chat/save/home/close/tech/unlock/confirm/cancel/launch/refresh/send/newgame/load/quit/delete）</summary>
        public static Sprite GetButtonIcon(string name) => Get("icon_" + name);

        // ==================== 核心：按文件名加载（带缓存） ====================

        /// <summary>
        /// 加载 Resources/Sprites/{fileName} 的首个 Sprite。
        /// 用 LoadAll[0]：兼容 Single/Multiple 两种 Sprite 导入模式，用户替换 PNG 后即便 meta 重置也健壮。
        /// 缺失返回 null（null 也缓存，避免重复告警）。
        /// </summary>
        public static Sprite Get(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            if (_cache.TryGetValue(fileName, out var cached)) return cached;

            Sprite sprite = null;
            var all = Resources.LoadAll<Sprite>(DIR + fileName);
            if (all != null && all.Length > 0) sprite = all[0];
            else Debug.LogWarning($"[SpriteRegistry] 占位图缺失或无 Sprite: {DIR}{fileName}");

            _cache[fileName] = sprite; // 含 null 缓存
            return sprite;
        }

        // ==================== 降级 helper ====================

        /// <summary>
        /// 给 Image 贴占位图；sprite 为 null 时保持原色块（降级）。
        /// 统一所有接入点的"有图贴图、无图留色"逻辑：
        /// 贴图时强制 color=white（避免色块 tint 污染贴图）+ type=Simple（防止 Sliced 拉伸异常）。
        /// </summary>
        public static void ApplySpriteOrColor(Image img, Sprite sprite)
        {
            if (img == null) return;
            if (sprite == null) return; // 降级：保留 img 原 color（色块）
            img.sprite = sprite;
            img.color = Color.white;
            img.type = Image.Type.Simple;
        }

        /// <summary>
        /// 贴皮肤（Sliced + tint）：用于面板/按钮背景。
        /// 有皮肤 → sprite + tint(着色) + Sliced(九宫格拉伸)；无皮肤 → 纯色 tint(降级)。
        /// 始终设 color=tint，保留各面板/按钮的原有色调区分。
        /// </summary>
        public static void ApplySkin(Image img, Sprite skin, Color tint)
        {
            if (img == null) return;
            img.color = tint;
            if (skin == null) return; // 降级：纯色 tint
            img.sprite = skin;
            img.type = Image.Type.Sliced;
        }
    }
}
