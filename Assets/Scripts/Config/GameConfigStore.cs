/// <summary>
/// 游戏配置文件读写（静态工具）
/// 负责把 GameConfig 序列化为 JSON 存到 persistentDataPath，以及读回。
/// 运行时单例(GameConfigManager)与编辑器窗口(EditorWindow)共用同一份文件，
/// 因此本类不依赖任何 MonoBehaviour / 单例，可在编辑器非播放模式直接调用。
///
/// 文件位置：{persistentDataPath}/game_config.json
/// </summary>
using System;
using System.IO;
using UnityEngine;

namespace GalaxyAgent.Config
{
    public static class GameConfigStore
    {
        /// <summary>配置文件名</summary>
        public const string CONFIG_FILE_NAME = "game_config.json";

        /// <summary>配置文件完整路径（编辑器与运行时一致）</summary>
        public static string GetPath()
        {
            return Path.Combine(Application.persistentDataPath, CONFIG_FILE_NAME);
        }

        /// <summary>
        /// 创建一份带默认值的配置（字段默认值取自 Constants）
        /// </summary>
        public static GameConfig CreateDefault()
        {
            return new GameConfig();
        }

        /// <summary>
        /// 从磁盘加载配置；文件不存在或解析失败时返回默认配置。
        /// </summary>
        public static GameConfig Load()
        {
            string path = GetPath();
            try
            {
                if (!File.Exists(path))
                {
                    Debug.Log($"[GameConfigStore] 配置文件不存在，使用默认配置: {path}");
                    return CreateDefault();
                }

                string json = File.ReadAllText(path);
                GameConfig config = JsonUtility.FromJson<GameConfig>(json);
                if (config == null)
                {
                    Debug.LogWarning("[GameConfigStore] 配置解析为空，使用默认配置");
                    return CreateDefault();
                }
                Debug.Log($"[GameConfigStore] 已加载配置: {path}");
                return config;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameConfigStore] 配置加载失败，使用默认配置: {e.Message}");
                return CreateDefault();
            }
        }

        /// <summary>
        /// 把配置写入磁盘（prettyPrint 便于手动编辑）
        /// </summary>
        public static void Save(GameConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[GameConfigStore] 保存失败：配置为空");
                return;
            }
            try
            {
                string path = GetPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonUtility.ToJson(config, true);
                File.WriteAllText(path, json);
                Debug.Log($"[GameConfigStore] 配置已保存: {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameConfigStore] 配置保存失败: {e.Message}");
            }
        }
    }
}
