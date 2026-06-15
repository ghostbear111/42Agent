/// <summary>
/// Mod 资源管理器 —— 让玩家用「游戏目录下的 Mod 文件」替换内置图片元素。
///
/// 核心思路：
/// 游戏打包为 exe 后，内置图片（Resources/Sprites/）被压缩进 *_Data/resources.assets，
/// 玩家无法直接修改。本类在「游戏目录（exe 旁边）」开一个 Mods 文件夹，
/// 玩家把同名 PNG 放进去，SpriteRegistry 加载时优先读 Mod，实现免打包换图。
///
/// 路径定位（关键）：
/// - 游戏目录 = Path.GetDirectoryName(Application.dataPath)：
///     打包后 Application.dataPath = "&lt;游戏目录&gt;/&lt;游戏名&gt;_Data"，上一级即 exe 旁边。
///     编辑器   Application.dataPath = "&lt;项目根&gt;/Assets"，上一级即项目根。
///   → Mod 目录统一放在「游戏目录/Mods」，编辑器下可测、打包后玩家直接在 exe 旁边看到。
/// - 模板源 = StreamingAssets/DefaultSprites：随包发布的内置原始 PNG，供"导出模板"复制。
///   用原始字节而非 Resources.Load+EncodeToPNG（占位图 isReadable=false 无法回读，且原始质量最高）。
///
/// 图片优先级（由 SpriteRegistry.Get 统一处理）：
///   Mod 目录同名 PNG  >  内置 Resources 占位图  >  null（降级色块）
///
/// 生效时机：重启游戏（SpriteRegistry 缓存是进程级，重启自动清空重读）。
///
/// 9-slice 皮肤（panel_bg/btn_bg）的 Mod 图按 Simple 拉伸处理（外部 PNG 无 border 信息）。
/// </summary>
using System.IO;
using UnityEngine;

namespace GalaxyAgent.Modding
{
    public static class ModManager
    {
        // ==================== 目录定位 ====================

        /// <summary>游戏目录（exe 旁边；编辑器下=项目根）。Mod 文件夹放在这里玩家最易找到。</summary>
        public static string GetGameDir()
            => Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;

        /// <summary>Mod 根目录：游戏目录/Mods</summary>
        public static string GetModDir() => Path.Combine(GetGameDir(), "Mods");

        /// <summary>Mod 图片目录：游戏目录/Mods/Sprites（玩家把同名 PNG 放这里替换内置图）</summary>
        public static string GetModSpritesDir() => Path.Combine(GetModDir(), "Sprites");

        /// <summary>内置模板源目录：StreamingAssets/DefaultSprites（随包发布的原始 PNG）</summary>
        public static string GetDefaultSpritesDir()
            => Path.Combine(Application.streamingAssetsPath, "DefaultSprites");

        // ==================== 目录初始化 ====================

        /// <summary>
        /// 确保 Mod 目录结构存在（Mods/ 和 Mods/Sprites/，递归创建）。
        /// 首次启动调用，让玩家进游戏目录就能看到 Mods/Sprites 空文件夹。
        /// </summary>
        public static void EnsureModDirs()
        {
            try { Directory.CreateDirectory(GetModSpritesDir()); }
            catch (System.Exception e) { Debug.LogError($"[ModManager] 创建 Mod 目录失败: {e.Message}"); }
        }

        /// <summary>
        /// 首次启动初始化：确保 Mods 目录存在，若 Mods/Sprites 还没有任何图则导出默认模板。
        /// 让玩家装好游戏进目录就能看到现成模板可改。已有玩家图则跳过（不覆盖）。
        /// </summary>
        public static void EnsureModSetup()
        {
            EnsureModDirs();
            string dir = GetModSpritesDir();
            if (!Directory.Exists(dir) || Directory.GetFiles(dir, "*.png").Length == 0)
                ExportDefaultTemplate(overwrite: false);
        }

        // ==================== Mod 图片加载 ====================

        /// <summary>
        /// 从 Mod 目录加载 {fileName}.png 为 Sprite。文件不存在或解析失败返回 null。
        /// 由 SpriteRegistry.Get 调用：返回非 null 即用 Mod 图，返回 null 则 fallback 内置 Resources。
        /// 用 Texture2D.LoadImage（自动按图像尺寸调整，兼容 PNG/JPG），Sprite.Create 居中锚点。
        /// </summary>
        public static Sprite TryLoadSprite(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            string path = Path.Combine(GetModSpritesDir(), fileName + ".png");
            if (!File.Exists(path)) return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes))
                {
                    Object.Destroy(tex);
                    Debug.LogWarning($"[ModManager] Mod 图片解析失败: {path}");
                    return null;
                }
                tex.hideFlags = HideFlags.HideAndDontSave; // 不进场景树、不进存盘、销毁不告警
                // 居中 pivot + ppu 100（UI Image 按 RectTransform 尺寸显示，ppu 不影响视觉）
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
                sprite.name = fileName;
                return sprite;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ModManager] 加载 Mod 图片异常 {path}: {e.Message}");
                return null;
            }
        }

        // ==================== 模板导出（给玩家现成可改的图）====================

        /// <summary>
        /// 导出默认模板：把内置原始 PNG 复制到 Mods/Sprites/，并在 Mods/ 生成命名说明 README。
        /// 玩家进 Mods/Sprites 即见全部可替换图，覆盖同名文件即生效（重启）。
        /// </summary>
        /// <param name="overwrite">true 覆盖已存在（强制刷新模板）；false 跳过已存在（保留玩家改动）</param>
        /// <returns>实际复制的文件数</returns>
        public static int ExportDefaultTemplate(bool overwrite = false)
        {
            EnsureModDirs();
            string srcDir = GetDefaultSpritesDir();
            string dstDir = GetModSpritesDir();

            if (!Directory.Exists(srcDir))
            {
                Debug.LogError($"[ModManager] 模板源缺失（StreamingAssets/DefaultSprites 未随包）: {srcDir}");
                return 0;
            }

            int count = 0;
            foreach (var src in Directory.GetFiles(srcDir, "*.png"))
            {
                string dst = Path.Combine(dstDir, Path.GetFileName(src));
                if (!overwrite && File.Exists(dst)) continue; // 非覆盖模式：保留玩家改动
                File.Copy(src, dst, overwrite);
                count++;
            }

            WriteReadme(); // 每次导出刷新说明（固定内容）
            Debug.Log($"[ModManager] 已导出 {count} 张模板图到 {dstDir}");
            return count;
        }

        /// <summary>在 Mods/ 写一份命名说明 README（每次导出覆盖，内容固定）</summary>
        private static void WriteReadme()
        {
            string readme = Path.Combine(GetModDir(), "README.txt");
            File.WriteAllText(readme, README_CONTENT);
        }

        // README 正文（玩家面向的中文命名说明）
        private const string README_CONTENT =
@"42agent Mod 换图说明
======================

把图片（PNG）放进「Mods/Sprites」文件夹，文件名必须与下表完全一致（含大小写），
重启游戏即可替换对应元素。

【Agent 头像】（建议 128x128 方图）
  avatar_scout.png   探索者
  avatar_worker.png  采集者
  avatar_guard.png   守卫
  avatar_base.png    基地

【资源图标】（64x64 起，等比缩放）
  icon_mineral.png 矿物    icon_crystal.png 晶体    icon_water.png 水
  icon_organic.png 有机    icon_ruin.png    遗迹

【天气图标】
  weather_clear.png 晴        weather_sandstorm.png 沙尘暴
  weather_acidrain.png 酸雨   weather_coldwave.png 寒潮
  weather_magneticstorm.png 磁暴   weather_blizzard.png 暴风雪

【场景背景】（建议 1920x1080，16:9）
  bg_mainmenu.png 主菜单全屏背景
  bg_mapgen.png   地图生成场景背景

【面板 / 按钮皮肤】（按 Simple 拉伸，建议做纯色或简单纹理）
  panel_bg.png 面板底纹（所有窗口/栏）
  btn_bg.png   按钮皮肤（所有按钮）

【功能按钮图标】（64x64）
  icon_pause 暂停        icon_config 配置      icon_chat LLM对话   icon_save 保存
  icon_home 返回菜单     icon_close 关闭        icon_tech 科技树    icon_unlock 解锁
  icon_confirm 确认      icon_cancel 取消       icon_launch 发射    icon_refresh 刷新
  icon_send 发送         icon_newgame 新游戏    icon_load 加载      icon_quit 退出
  icon_delete 删除

======================
规则
======================
- 格式：PNG（JPG 亦可）。尺寸不限，按 UI 槽位自动缩放。
- 删除某张 PNG → 自动回退到游戏内置图（不会崩溃）。
- 改图后需【重启游戏】生效（运行中改不即时刷新）。
- 命名必须完全一致：avatar_scout.png（不是 scout.png、不是 Avatar_Scout.png）。

======================
快速开始
======================
1. 本文件夹（Mods/Sprites）已含全部模板图，可直接用你的图覆盖同名文件。
2. 重启游戏即可看到效果。
3. 想恢复默认？删掉 Mods/Sprites 里你加的图，或整个 Sprites 文件夹。
";
    }
}
